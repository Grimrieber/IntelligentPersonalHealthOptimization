using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels;

public partial class BarcodeScannerViewModel : BaseViewModel
{
    private readonly IFoodService _foodService;

    public BarcodeScannerViewModel(IFoodService foodService)
    {
        _foodService = foodService;
        Title = "Scan Barcode";
    }

    [ObservableProperty]
    private bool _isScanning = true;

    [ObservableProperty]
    private bool _isTorchOn;

    [ObservableProperty]
    private string _scanStatusText = "Point camera at barcode";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _lastScannedBarcode = string.Empty;

    [ObservableProperty]
    private bool _foodFound;

    [ObservableProperty]
    private string _foundFoodName = string.Empty;

    [ObservableProperty]
    private string _foundFoodCalories = string.Empty;

    [RelayCommand]
    private async Task OnBarcodeDetectedAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || IsProcessing) return;
        if (barcode == LastScannedBarcode) return;

        IsProcessing = true;
        IsScanning = false;
        LastScannedBarcode = barcode;
        ScanStatusText = "Looking up barcode...";

        try
        {
            var food = await _foodService.GetFoodByBarcodeAsync(barcode);

            if (food != null)
            {
                FoodFound = true;
                FoundFoodName = string.IsNullOrEmpty(food.Brand)
                    ? food.Name
                    : $"{food.Name} ({food.Brand})";
                FoundFoodCalories = $"{food.CaloriesPer100g:F0} kcal/100g";
                ScanStatusText = "Food found!";

                await Shell.Current.GoToAsync($"../{RouteConstants.AddFoodEntry}?foodId={food.Id}");
            }
            else
            {
                FoodFound = false;
                ScanStatusText = "Food not found";

                bool addManually = await Shell.Current.DisplayAlert(
                    "Food Not Found",
                    "This barcode is not in our database. Would you like to add this food manually?",
                    "Add Manually", "Scan Again");

                if (addManually)
                {
                    await Shell.Current.GoToAsync($"../{RouteConstants.AddCustomFood}?barcode={barcode}");
                }
                else
                {
                    ResetScanner();
                }
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("Barcode lookup", ex);
            ScanStatusText = "Error looking up barcode. Try again.";
            ResetScanner();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ToggleTorch()
    {
        IsTorchOn = !IsTorchOn;
    }

    [RelayCommand]
    private void ResetScanner()
    {
        IsScanning = true;
        IsProcessing = false;
        FoodFound = false;
        LastScannedBarcode = string.Empty;
        FoundFoodName = string.Empty;
        FoundFoodCalories = string.Empty;
        ScanStatusText = "Point camera at barcode";
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
