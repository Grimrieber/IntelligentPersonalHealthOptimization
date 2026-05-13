-- ============================================================
-- Views: Make staging tables look like their production equivalents
-- Purpose: Proof of concept before merging staging → production
--
-- Linking logic:
--   Staging tables link on SpoonacularID
--   Production tables link on RecipeID
--   These views use StagingID as the RecipeID stand-in,
--   and join child tables via SpoonacularID → Staging_Recipes.SpoonacularID
-- ============================================================

-- ============================================================
-- 1. vw_Staging_Recipes
--    Maps staging_Recipes → Recipes shape
-- ============================================================
IF OBJECT_ID('dbo.vw_Staging_Recipes', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Staging_Recipes;
GO

CREATE VIEW dbo.vw_Staging_Recipes
AS
SELECT
    sr.StagingID                          AS RecipeID,
    ISNULL(c.CategoryID, 0)              AS CategoryID,
    sr.RecipeName,
    sr.PrepTime,
    sr.CookTime,
    NULL                                  AS RestTime,
    sr.Servings,
    sr.Difficulty,
    sr.Source,
    sr.Notes,
    NULL                                  AS RecipeImage,
    NULL                                  AS Rating,
    CAST(0 AS BIT)                        AS IsFavorite,
    sr.ImportedAt                          AS DateAdded,
    NULL                                  AS LastMadeOn,
    -- Extra staging columns kept for reference during review
    sr.SpoonacularID,
    sr.SourceUrl,
    sr.ImageUrl,
    sr.DietLabels,
    sr.DishTypes,
    sr.Cuisines,
    sr.IsApproved,
    sr.CategoryName                        AS StagingCategoryName
FROM Staging_Recipes sr
LEFT JOIN Categories c
    ON c.CategoryName = sr.CategoryName;
GO


-- ============================================================
-- 2. vw_Staging_RecipeIngredients
--    Maps staging_RecipeIngredients → RecipeIngredients shape
--    Links through Staging_Recipes.SpoonacularID to get StagingID as RecipeID
-- ============================================================
IF OBJECT_ID('dbo.vw_Staging_RecipeIngredients', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Staging_RecipeIngredients;
GO

CREATE VIEW dbo.vw_Staging_RecipeIngredients
AS
SELECT
    si.ID                                  AS IngredientID,
    sr.StagingID                           AS RecipeID,
    si.SortOrder,
    si.IngredientGroup,
    si.Description,
    -- Extra staging columns kept for reference
    si.OriginalName,
    si.Amount,
    si.Unit,
    si.AisleName
FROM Staging_RecipeIngredients si
INNER JOIN Staging_Recipes sr
    ON sr.SpoonacularID = si.SpoonacularRecipeID;
GO


-- ============================================================
-- 3. vw_Staging_RecipeDirections
--    Maps staging_RecipeDirections → RecipeDirections shape
-- ============================================================
IF OBJECT_ID('dbo.vw_Staging_RecipeDirections', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Staging_RecipeDirections;
GO

CREATE VIEW dbo.vw_Staging_RecipeDirections
AS
SELECT
    sd.ID                                  AS DirectionID,
    sr.StagingID                           AS RecipeID,
    sd.StepNumber,
    sd.DirectionGroup,
    sd.Instruction
FROM Staging_RecipeDirections sd
INNER JOIN Staging_Recipes sr
    ON sr.SpoonacularID = sd.SpoonacularRecipeID;
GO


-- ============================================================
-- 4. vw_Staging_RecipeNutrition
--    Maps staging_RecipeNutrition → RecipeNutrition shape
-- ============================================================
IF OBJECT_ID('dbo.vw_Staging_RecipeNutrition', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Staging_RecipeNutrition;
GO

CREATE VIEW dbo.vw_Staging_RecipeNutrition
AS
SELECT
    sn.ID                                  AS NutritionID,
    sr.StagingID                           AS RecipeID,
    sn.CaloriesPerServing,
    sn.TotalFatGrams,
    sn.SaturatedFatGrams,
    sn.CholesterolMg,
    sn.SodiumMg,
    sn.TotalCarbsGrams,
    sn.FiberGrams,
    sn.SugarGrams,
    sn.ProteinGrams,
    sn.ServingSizeNote
FROM Staging_RecipeNutrition sn
INNER JOIN Staging_Recipes sr
    ON sr.SpoonacularID = sn.SpoonacularRecipeID;
GO


-- ============================================================
-- 5. vw_Staging_MissingCategories
--    Shows staging category names that don't exist in production,
--    with recipe counts so you can prioritize
-- ============================================================
IF OBJECT_ID('dbo.vw_Staging_MissingCategories', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Staging_MissingCategories;
GO

CREATE VIEW dbo.vw_Staging_MissingCategories
AS
SELECT
    sr.CategoryName                        AS StagingCategoryName,
    COUNT(*)                               AS RecipeCount,
    -- Suggest closest production match if one looks similar
    (SELECT TOP 1 c.CategoryName
     FROM Categories c
     WHERE sr.CategoryName LIKE '%' + c.CategoryName + '%'
        OR c.CategoryName LIKE '%' + sr.CategoryName + '%'
    )                                      AS PossibleMatch
FROM Staging_Recipes sr
LEFT JOIN Categories c
    ON c.CategoryName = sr.CategoryName
WHERE c.CategoryID IS NULL
GROUP BY sr.CategoryName;
GO


-- ============================================================
-- BONUS: Proof-of-concept query
-- Join all 4 views together just like you would the production tables
-- to verify everything links correctly
-- ============================================================
/*
SELECT
    r.RecipeID,
    r.RecipeName,
    r.CategoryID,
    r.PrepTime,
    r.CookTime,
    r.Servings,
    r.Difficulty,
    r.Source,
    r.IsApproved,

    -- Ingredients
    i.IngredientID,
    i.SortOrder,
    i.Description       AS IngredientDescription,
    i.Amount,
    i.Unit,

    -- Directions
    d.DirectionID,
    d.StepNumber,
    d.Instruction,

    -- Nutrition
    n.CaloriesPerServing,
    n.ProteinGrams,
    n.TotalCarbsGrams,
    n.TotalFatGrams,
    n.FiberGrams,
    n.SugarGrams

FROM vw_Staging_Recipes r
LEFT JOIN vw_Staging_RecipeIngredients i  ON i.RecipeID = r.RecipeID
LEFT JOIN vw_Staging_RecipeDirections d   ON d.RecipeID = r.RecipeID
LEFT JOIN vw_Staging_RecipeNutrition n    ON n.RecipeID = r.RecipeID
WHERE r.RecipeID = 1   -- swap in any StagingID
ORDER BY i.SortOrder, d.StepNumber;
*/
