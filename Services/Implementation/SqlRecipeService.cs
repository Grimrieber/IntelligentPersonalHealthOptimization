#if WINDOWS
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models.Recipe;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

/// <summary>
/// Reads recipes directly from MSSQL RecipeDB.
/// Swap this for ApiRecipeService when moving to online mode.
/// </summary>
public class SqlRecipeService : IRecipeService
{
    private string ConnectionString => AppConstants.RecipeDbConnectionString;

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<RecipeCategory>> GetCategoriesAsync()
    {
        var categories = new List<RecipeCategory>();

        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT c.CategoryID, c.CategoryName, COUNT(r.RecipeID) AS RecipeCount
            FROM Categories c
            LEFT JOIN Recipes r ON c.CategoryID = r.CategoryID
            GROUP BY c.CategoryID, c.CategoryName
            ORDER BY c.CategoryName";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            categories.Add(new RecipeCategory
            {
                CategoryID = reader.GetInt32(0),
                CategoryName = reader.GetString(1),
                RecipeCount = reader.GetInt32(2)
            });
        }

        return categories;
    }

    public async Task<List<RecipeItem>> GetRecipesByCategoryAsync(int categoryId)
    {
        var recipes = new List<RecipeItem>();

        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT r.RecipeID, r.CategoryID, r.RecipeName, r.PrepTime, r.CookTime,
                   r.RestTime, r.Servings, r.Difficulty, r.Rating, r.IsFavorite,
                   r.DateAdded, c.CategoryName,
                   (SELECT COUNT(*) FROM RecipeIngredients WHERE RecipeID = r.RecipeID) AS IngredientCount,
                   (SELECT COUNT(*) FROM RecipeDirections WHERE RecipeID = r.RecipeID) AS DirectionCount,
                   CASE WHEN EXISTS(SELECT 1 FROM RecipeNutrition WHERE RecipeID = r.RecipeID) THEN 1 ELSE 0 END AS HasNutrition
            FROM Recipes r
            JOIN Categories c ON r.CategoryID = c.CategoryID
            WHERE r.CategoryID = @CategoryID
            ORDER BY r.RecipeName";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@CategoryID", categoryId);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            recipes.Add(MapRecipeItem(reader));
        }

        return recipes;
    }

    public async Task<List<RecipeItem>> SearchRecipesAsync(string searchTerm)
    {
        var recipes = new List<RecipeItem>();

        if (string.IsNullOrWhiteSpace(searchTerm))
            return recipes;

        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT DISTINCT r.RecipeID, r.CategoryID, r.RecipeName, r.PrepTime, r.CookTime,
                   r.RestTime, r.Servings, r.Difficulty, r.Rating, r.IsFavorite,
                   r.DateAdded, c.CategoryName,
                   (SELECT COUNT(*) FROM RecipeIngredients WHERE RecipeID = r.RecipeID) AS IngredientCount,
                   (SELECT COUNT(*) FROM RecipeDirections WHERE RecipeID = r.RecipeID) AS DirectionCount,
                   CASE WHEN EXISTS(SELECT 1 FROM RecipeNutrition WHERE RecipeID = r.RecipeID) THEN 1 ELSE 0 END AS HasNutrition
            FROM Recipes r
            JOIN Categories c ON r.CategoryID = c.CategoryID
            LEFT JOIN RecipeIngredients ri ON r.RecipeID = ri.RecipeID
            WHERE r.RecipeName LIKE @Search
               OR ri.Description LIKE @Search
               OR c.CategoryName LIKE @Search
            ORDER BY r.RecipeName";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            recipes.Add(MapRecipeItem(reader));
        }

        return recipes;
    }

    public async Task<RecipeDetail?> GetRecipeDetailAsync(int recipeId)
    {
        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var detail = new RecipeDetail();

        // Recipe header
        const string recipeSql = @"
            SELECT r.RecipeID, r.CategoryID, r.RecipeName, r.PrepTime, r.CookTime,
                   r.RestTime, r.Servings, r.Difficulty, r.Source, r.Notes,
                   r.Rating, r.IsFavorite, r.DateAdded, r.LastMadeOn, c.CategoryName
            FROM Recipes r
            JOIN Categories c ON r.CategoryID = c.CategoryID
            WHERE r.RecipeID = @RecipeID";

        using (var cmd = new SqlCommand(recipeSql, conn))
        {
            cmd.Parameters.AddWithValue("@RecipeID", recipeId);
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            detail.Recipe = new RecipeItem
            {
                RecipeID = reader.GetInt32(0),
                CategoryID = reader.GetInt32(1),
                RecipeName = reader.GetString(2),
                PrepTime = reader.IsDBNull(3) ? null : reader.GetString(3),
                CookTime = reader.IsDBNull(4) ? null : reader.GetString(4),
                RestTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                Servings = reader.IsDBNull(6) ? null : reader.GetString(6),
                Difficulty = reader.IsDBNull(7) ? null : reader.GetString(7),
                Source = reader.IsDBNull(8) ? null : reader.GetString(8),
                Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                Rating = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                IsFavorite = reader.GetBoolean(11),
                DateAdded = reader.GetDateTime(12),
                LastMadeOn = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                CategoryName = reader.GetString(14)
            };
        }

        // Ingredients
        const string ingredientsSql = @"
            SELECT IngredientID, RecipeID, SortOrder, IngredientGroup, Description
            FROM RecipeIngredients
            WHERE RecipeID = @RecipeID
            ORDER BY SortOrder";

        using (var cmd = new SqlCommand(ingredientsSql, conn))
        {
            cmd.Parameters.AddWithValue("@RecipeID", recipeId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detail.Ingredients.Add(new RecipeIngredient
                {
                    IngredientID = reader.GetInt32(0),
                    RecipeID = reader.GetInt32(1),
                    SortOrder = reader.GetInt32(2),
                    IngredientGroup = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Description = reader.GetString(4)
                });
            }
        }

        // Directions
        const string directionsSql = @"
            SELECT DirectionID, RecipeID, StepNumber, DirectionGroup, Instruction
            FROM RecipeDirections
            WHERE RecipeID = @RecipeID
            ORDER BY StepNumber";

        using (var cmd = new SqlCommand(directionsSql, conn))
        {
            cmd.Parameters.AddWithValue("@RecipeID", recipeId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detail.Directions.Add(new RecipeDirection
                {
                    DirectionID = reader.GetInt32(0),
                    RecipeID = reader.GetInt32(1),
                    StepNumber = reader.GetInt32(2),
                    DirectionGroup = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Instruction = reader.GetString(4)
                });
            }
        }

        // Nutrition
        const string nutritionSql = @"
            SELECT NutritionID, RecipeID, CaloriesPerServing, TotalFatGrams, SaturatedFatGrams,
                   CholesterolMg, SodiumMg, TotalCarbsGrams, FiberGrams, SugarGrams,
                   ProteinGrams, ServingSizeNote
            FROM RecipeNutrition
            WHERE RecipeID = @RecipeID";

        using (var cmd = new SqlCommand(nutritionSql, conn))
        {
            cmd.Parameters.AddWithValue("@RecipeID", recipeId);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                detail.Nutrition = new RecipeNutrition
                {
                    NutritionID = reader.GetInt32(0),
                    RecipeID = reader.GetInt32(1),
                    CaloriesPerServing = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    TotalFatGrams = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                    SaturatedFatGrams = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    CholesterolMg = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    SodiumMg = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    TotalCarbsGrams = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                    FiberGrams = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                    SugarGrams = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                    ProteinGrams = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                    ServingSizeNote = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
            }
        }

        // Tags
        const string tagsSql = @"
            SELECT TagID, RecipeID, TagName
            FROM RecipeTags
            WHERE RecipeID = @RecipeID";

        using (var cmd = new SqlCommand(tagsSql, conn))
        {
            cmd.Parameters.AddWithValue("@RecipeID", recipeId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detail.Tags.Add(new RecipeTag
                {
                    TagID = reader.GetInt32(0),
                    RecipeID = reader.GetInt32(1),
                    TagName = reader.GetString(2)
                });
            }
        }

        return detail;
    }

    private static RecipeItem MapRecipeItem(SqlDataReader reader)
    {
        return new RecipeItem
        {
            RecipeID = reader.GetInt32(0),
            CategoryID = reader.GetInt32(1),
            RecipeName = reader.GetString(2),
            PrepTime = reader.IsDBNull(3) ? null : reader.GetString(3),
            CookTime = reader.IsDBNull(4) ? null : reader.GetString(4),
            RestTime = reader.IsDBNull(5) ? null : reader.GetString(5),
            Servings = reader.IsDBNull(6) ? null : reader.GetString(6),
            Difficulty = reader.IsDBNull(7) ? null : reader.GetString(7),
            Rating = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            IsFavorite = reader.GetBoolean(9),
            DateAdded = reader.GetDateTime(10),
            CategoryName = reader.GetString(11),
            IngredientCount = reader.GetInt32(12),
            DirectionCount = reader.GetInt32(13),
            HasNutrition = reader.GetInt32(14) == 1
        };
    }
}
#endif
