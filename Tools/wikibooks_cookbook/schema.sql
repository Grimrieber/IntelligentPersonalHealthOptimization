-- Wikibooks Cookbook staging tables in RecipeDB.
-- Run once to create. Safe to re-run (uses IF NOT EXISTS guards).

IF OBJECT_ID('WIKIBOOKS_Categories', 'U') IS NULL
BEGIN
    CREATE TABLE WIKIBOOKS_Categories (
        WikibooksCategoryID  INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName         NVARCHAR(200) NOT NULL UNIQUE,
        CategoryUrl          NVARCHAR(500) NULL,
        DateImported         DATETIME NOT NULL DEFAULT GETDATE()
    );
END;

IF OBJECT_ID('WIKIBOOKS_Recipes', 'U') IS NULL
BEGIN
    CREATE TABLE WIKIBOOKS_Recipes (
        WikibooksRecipeID    INT IDENTITY(1,1) PRIMARY KEY,
        WikibooksCategoryID  INT NOT NULL
            FOREIGN KEY REFERENCES WIKIBOOKS_Categories(WikibooksCategoryID),
        RecipeName           NVARCHAR(500) NOT NULL,
        PrepTime             NVARCHAR(100) NULL,
        CookTime             NVARCHAR(100) NULL,
        RestTime             NVARCHAR(100) NULL,
        Servings             NVARCHAR(50)  NULL,
        Difficulty           NVARCHAR(50)  NULL,
        Source               NVARCHAR(500) NOT NULL,
        Notes                NVARCHAR(MAX) NULL,
        SourceFilename       NVARCHAR(200) NULL,
        DateImported         DATETIME NOT NULL DEFAULT GETDATE(),
        IsExcluded           BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_Wikibooks_Recipes_Name ON WIKIBOOKS_Recipes(RecipeName);
    CREATE INDEX IX_Wikibooks_Recipes_Category ON WIKIBOOKS_Recipes(WikibooksCategoryID);
END;

IF OBJECT_ID('WIKIBOOKS_RecipeIngredients', 'U') IS NULL
BEGIN
    CREATE TABLE WIKIBOOKS_RecipeIngredients (
        WikibooksIngredientID INT IDENTITY(1,1) PRIMARY KEY,
        WikibooksRecipeID     INT NOT NULL
            FOREIGN KEY REFERENCES WIKIBOOKS_Recipes(WikibooksRecipeID) ON DELETE CASCADE,
        SortOrder             INT NOT NULL,
        IngredientGroup       NVARCHAR(200) NULL,
        Description           NVARCHAR(500) NOT NULL
    );
    CREATE INDEX IX_Wikibooks_Ingredients_Recipe ON WIKIBOOKS_RecipeIngredients(WikibooksRecipeID);
END;

IF OBJECT_ID('WIKIBOOKS_RecipeDirections', 'U') IS NULL
BEGIN
    CREATE TABLE WIKIBOOKS_RecipeDirections (
        WikibooksDirectionID INT IDENTITY(1,1) PRIMARY KEY,
        WikibooksRecipeID    INT NOT NULL
            FOREIGN KEY REFERENCES WIKIBOOKS_Recipes(WikibooksRecipeID) ON DELETE CASCADE,
        StepNumber           INT NOT NULL,
        DirectionGroup       NVARCHAR(200) NULL,
        Instruction          NVARCHAR(MAX) NOT NULL
    );
    CREATE INDEX IX_Wikibooks_Directions_Recipe ON WIKIBOOKS_RecipeDirections(WikibooksRecipeID);
END;

IF OBJECT_ID('WIKIBOOKS_RecipeNutrition', 'U') IS NULL
BEGIN
    CREATE TABLE WIKIBOOKS_RecipeNutrition (
        WikibooksNutritionID INT IDENTITY(1,1) PRIMARY KEY,
        WikibooksRecipeID    INT NOT NULL UNIQUE
            FOREIGN KEY REFERENCES WIKIBOOKS_Recipes(WikibooksRecipeID) ON DELETE CASCADE,
        CaloriesPerServing   INT            NULL,
        TotalFatGrams        DECIMAL(8,2)   NULL,
        SaturatedFatGrams    DECIMAL(8,2)   NULL,
        CholesterolMg        DECIMAL(8,2)   NULL,
        SodiumMg             DECIMAL(8,2)   NULL,
        TotalCarbsGrams      DECIMAL(8,2)   NULL,
        FiberGrams           DECIMAL(8,2)   NULL,
        SugarGrams           DECIMAL(8,2)   NULL,
        ProteinGrams         DECIMAL(8,2)   NULL,
        ServingSizeNote      NVARCHAR(200)  NULL,
        IngredientMatchRate  DECIMAL(5,2)   NULL,
        DateComputed         DATETIME       NULL
    );
END;
