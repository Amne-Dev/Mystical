using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Mystical.WinUI.Models;
using Mystical.WinUI.Services;

namespace Mystical.WinUI.ViewModels;

public sealed class DealsViewModel : ViewModelBase
{
    private readonly IDealsService _dealsService;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _isLoading;
    private string _statusMessage = "Ready";

    public ObservableCollection<DealItem> MassiveDiscountDeals { get; } = new();

    public ObservableCollection<DealItem> FreeNowDeals { get; } = new();

    public ObservableCollection<DealItem> FreeSoonDeals { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public Visibility FreeNowVisibility => FreeNowDeals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FreeSoonVisibility => FreeSoonDeals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RefreshDealsCommand => _refreshCommand;

    public DealsViewModel(IDealsService dealsService)
    {
        _dealsService = dealsService;
        _refreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Fetching the latest deals...";

        try
        {
            var snapshot = await _dealsService.FetchDealsAsync();

            ReplaceDeals(MassiveDiscountDeals, snapshot.MassiveDiscountDeals);
            ReplaceDeals(FreeNowDeals, snapshot.FreeNowDeals);
            ReplaceDeals(FreeSoonDeals, snapshot.FreeSoonDeals);

            OnPropertyChanged(nameof(FreeNowVisibility));
            OnPropertyChanged(nameof(FreeSoonVisibility));

            StatusMessage = $"Updated {snapshot.FetchedAt.LocalDateTime:g}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load deals: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void ReplaceDeals(ObservableCollection<DealItem> target, IReadOnlyList<DealItem> source)
    {
        target.Clear();
        foreach (var deal in source)
        {
            target.Add(deal);
        }
    }
}
