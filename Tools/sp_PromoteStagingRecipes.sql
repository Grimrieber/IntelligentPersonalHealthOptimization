-- ============================================================
-- sp_PromoteStagingRecipes
-- 1. Backs up all 4 production recipe tables (timestamped)
-- 2. Promotes staging recipes into production
-- 3. Preserves parent-child relationships by using SpoonacularID
--    as the bridge and SCOPE_IDENTITY() to capture new RecipeIDs
--
-- Skips recipes already in production (by RecipeName match)
-- Wrapped in a transaction — all or nothing
-- ============================================================
IF OBJECT_ID('dbo.sp_PromoteStagingRecipes', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_PromoteStagingRecipes;
GO

CREATE PROCEDURE dbo.sp_PromoteStagingRecipes
AS
BEGIN
    SET NOCOUNT ON;

    -- Track what we promote
    DECLARE @Results TABLE (
        SpoonacularID   INT,
        NewRecipeID     INT,
        RecipeName      NVARCHAR(500),
        CategoryName    NVARCHAR(100),
        Ingredients     INT,
        Directions      INT,
        HasNutrition    BIT
    );

    DECLARE @SpoonId        INT;
    DECLARE @NewRecipeID    INT;
    DECLARE @RecipeName     NVARCHAR(500);
    DECLARE @CategoryName   NVARCHAR(100);
    DECLARE @IngCount       INT;
    DECLARE @DirCount       INT;
    DECLARE @HasNutrition   BIT;

    -- ========================================================
    -- STEP 1: Backup production tables
    -- ========================================================
    DECLARE @BackupSuffix NVARCHAR(20) = FORMAT(GETDATE(), 'yyyyMMdd_HHmmss');
    DECLARE @SQL NVARCHAR(MAX);

    PRINT '** Creating backups with suffix: ' + @BackupSuffix + ' **';

    SET @SQL = 'SELECT * INTO Recipes_Backup_' + @BackupSuffix + ' FROM Recipes;';
    EXEC sp_executesql @SQL;
    PRINT '   - Recipes backed up';

    SET @SQL = 'SELECT * INTO RecipeIngredients_Backup_' + @BackupSuffix + ' FROM RecipeIngredients;';
    EXEC sp_executesql @SQL;
    PRINT '   - RecipeIngredients backed up';

    SET @SQL = 'SELECT * INTO RecipeDirections_Backup_' + @BackupSuffix + ' FROM RecipeDirections;';
    EXEC sp_executesql @SQL;
    PRINT '   - RecipeDirections backed up';

    SET @SQL = 'SELECT * INTO RecipeNutrition_Backup_' + @BackupSuffix + ' FROM RecipeNutrition;';
    EXEC sp_executesql @SQL;
    PRINT '   - RecipeNutrition backed up';

    PRINT '** All backups created **';
    PRINT '';

    -- ========================================================
    -- STEP 1b: Remap staging category names to production names
    -- ========================================================
    UPDATE Staging_Recipes SET CategoryName = 'Vegetables and Sides' WHERE CategoryName = 'Side Dishes';
    UPDATE Staging_Recipes SET CategoryName = 'Soup'                 WHERE CategoryName = 'Soups';

    PRINT '** Category remappings applied **';
    PRINT '';

    -- ========================================================
    -- STEP 2: Promote staging → production
    -- ========================================================
    BEGIN TRANSACTION;

    BEGIN TRY

        DECLARE recipe_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT sr.SpoonacularID
            FROM Staging_Recipes sr
            WHERE NOT EXISTS (
                SELECT 1 FROM Recipes r
                WHERE r.RecipeName = sr.RecipeName
            );

        OPEN recipe_cursor;
        FETCH NEXT FROM recipe_cursor INTO @SpoonId;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            -- 1. Insert recipe, capture new RecipeID
            INSERT INTO Recipes (
                CategoryID, RecipeName, PrepTime, CookTime, RestTime,
                Servings, Difficulty, Source, Notes,
                RecipeImage, Rating, IsFavorite, DateAdded, LastMadeOn
            )
            SELECT
                ISNULL(c.CategoryID, 0),
                sr.RecipeName, sr.PrepTime, sr.CookTime, NULL,
                sr.Servings, sr.Difficulty, sr.Source, sr.Notes,
                NULL, NULL, 0, GETDATE(), NULL
            FROM Staging_Recipes sr
            LEFT JOIN Categories c ON c.CategoryName = sr.CategoryName
            WHERE sr.SpoonacularID = @SpoonId;

            SET @NewRecipeID = SCOPE_IDENTITY();

            -- 2. Insert ingredients linked to new RecipeID
            INSERT INTO RecipeIngredients (RecipeID, SortOrder, IngredientGroup, Description)
            SELECT @NewRecipeID, si.SortOrder, si.IngredientGroup, si.Description
            FROM Staging_RecipeIngredients si
            WHERE si.SpoonacularRecipeID = @SpoonId;

            SET @IngCount = @@ROWCOUNT;

            -- 3. Insert directions linked to new RecipeID
            INSERT INTO RecipeDirections (RecipeID, StepNumber, DirectionGroup, Instruction)
            SELECT @NewRecipeID, sd.StepNumber, sd.DirectionGroup, sd.Instruction
            FROM Staging_RecipeDirections sd
            WHERE sd.SpoonacularRecipeID = @SpoonId;

            SET @DirCount = @@ROWCOUNT;

            -- 4. Insert nutrition linked to new RecipeID
            INSERT INTO RecipeNutrition (
                RecipeID, CaloriesPerServing, TotalFatGrams, SaturatedFatGrams,
                CholesterolMg, SodiumMg, TotalCarbsGrams, FiberGrams,
                SugarGrams, ProteinGrams, ServingSizeNote
            )
            SELECT
                @NewRecipeID, sn.CaloriesPerServing, sn.TotalFatGrams, sn.SaturatedFatGrams,
                sn.CholesterolMg, sn.SodiumMg, sn.TotalCarbsGrams, sn.FiberGrams,
                sn.SugarGrams, sn.ProteinGrams, sn.ServingSizeNote
            FROM Staging_RecipeNutrition sn
            WHERE sn.SpoonacularRecipeID = @SpoonId;

            SET @HasNutrition = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;

            -- 5. Log result
            SELECT @RecipeName = RecipeName, @CategoryName = CategoryName
            FROM Staging_Recipes WHERE SpoonacularID = @SpoonId;

            INSERT INTO @Results VALUES (@SpoonId, @NewRecipeID, @RecipeName, @CategoryName, @IngCount, @DirCount, @HasNutrition);

            FETCH NEXT FROM recipe_cursor INTO @SpoonId;
        END;

        CLOSE recipe_cursor;
        DEALLOCATE recipe_cursor;

        -- ========================================================
        -- STEP 3: Output results
        -- ========================================================
        SELECT SpoonacularID, NewRecipeID, RecipeName, CategoryName,
               Ingredients, Directions,
               CASE WHEN HasNutrition = 1 THEN 'Yes' ELSE 'No' END AS HasNutrition
        FROM @Results
        ORDER BY NewRecipeID;

        SELECT
            COUNT(*)                           AS TotalPromoted,
            SUM(Ingredients)                   AS TotalIngredients,
            SUM(Directions)                    AS TotalDirections,
            SUM(CAST(HasNutrition AS INT))     AS WithNutrition
        FROM @Results;

        COMMIT TRANSACTION;

        PRINT '';
        PRINT '** COMMITTED — Staging recipes promoted to production. **';
        PRINT '** Backup tables: **';
        PRINT '   - Recipes_Backup_' + @BackupSuffix;
        PRINT '   - RecipeIngredients_Backup_' + @BackupSuffix;
        PRINT '   - RecipeDirections_Backup_' + @BackupSuffix;
        PRINT '   - RecipeNutrition_Backup_' + @BackupSuffix;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        IF CURSOR_STATUS('local', 'recipe_cursor') >= 0
        BEGIN
            CLOSE recipe_cursor;
            DEALLOCATE recipe_cursor;
        END;

        -- Backups are preserved even on failure so you can inspect state
        PRINT '** ERROR — Transaction rolled back. Backups preserved for inspection. **';
        PRINT '** Backup suffix: ' + @BackupSuffix + ' **';

        DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrSev INT = ERROR_SEVERITY();
        DECLARE @ErrState INT = ERROR_STATE();
        RAISERROR(@ErrMsg, @ErrSev, @ErrState);
    END CATCH;
END;
GO
