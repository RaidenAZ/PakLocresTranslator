using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UEPakTool.Gui.Models;
using UEPakTool.Gui.ViewModels;

namespace UEPakTool.Gui.Views;

public partial class TranslationPage : UserControl
{
    private TranslationViewModel? _vm;
    private PropertyChangedEventHandler? _highlightHandler;

    public TranslationPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TranslationViewModel oldVm)
        {
            oldVm.RecordSourceChanged -= OnRecordSourceChanged;
            if (_highlightHandler != null)
                oldVm.PropertyChanged -= _highlightHandler;
        }

        if (e.NewValue is TranslationViewModel newVm)
        {
            _vm = newVm;
            newVm.RecordSourceChanged += OnRecordSourceChanged;

            _highlightHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(TranslationViewModel.HighlightedIndex))
                    Dispatcher.Invoke(RefreshAllVisibleRows);
                else if (args.PropertyName == nameof(TranslationViewModel.IsTranslating))
                {
                    TranslateProgress.Visibility = newVm.IsTranslating
                        ? Visibility.Visible : Visibility.Collapsed;
                    CancelBtn.Visibility = newVm.IsTranslating
                        ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            newVm.PropertyChanged += _highlightHandler;
        }
    }

    private void OnRecordSourceChanged(TranslationRecord record)
    {
        Dispatcher.Invoke(() =>
        {
            var container = TranslationGrid.ItemContainerGenerator.ContainerFromItem(record) as DataGridRow;
            if (container != null)
                ApplyRowColor(container, record.Source);
        });
    }

    private void RefreshAllVisibleRows()
    {
        if (_vm == null) return;
        foreach (var item in TranslationGrid.Items)
        {
            if (item is TranslationRecord record)
            {
                var container = TranslationGrid.ItemContainerGenerator.ContainerFromItem(record) as DataGridRow;
                if (container != null)
                    RefreshHighlight(container, record, _vm);
            }
        }
    }

    private void TranslationGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not TranslationRecord record || _vm == null) return;
        RefreshHighlight(e.Row, record, _vm);
    }

    private void TranslationGrid_UnloadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.ClearValue(DataGridRow.BackgroundProperty);
    }

    private static void ApplyRowColor(DataGridRow row, TranslationSource source)
    {
        row.Background = source switch
        {
            TranslationSource.AiGenerated => new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD)),
            TranslationSource.ManuallyEntered => new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)),
            TranslationSource.ManuallyEdited => new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xE0)),
            _ => Brushes.White
        };
    }

    private static void RefreshHighlight(DataGridRow row, TranslationRecord record, TranslationViewModel vm)
    {
        if (vm.HighlightedIndex < 0)
        {
            ApplyRowColor(row, record.Source);
            return;
        }

        var idx = row.GetIndex();
        if (idx == vm.HighlightedIndex)
            row.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF9, 0xC4));
        else
            ApplyRowColor(row, record.Source);
    }

    private void TranslationGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Key\t原文\t译文");

            foreach (var item in TranslationGrid.SelectedItems)
            {
                if (item is TranslationRecord r)
                    sb.AppendLine($"{r.DisplayKey}\t{r.Original}\t{r.Translated}");
            }

            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());

            e.Handled = true;
        }
    }

    private void NextMatch_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_vm == null) return;
        var idx = _vm.GetNextMatchIndex();
        if (idx.HasValue)
        {
            TranslationGrid.SelectedIndex = idx.Value;
            TranslationGrid.ScrollIntoView(TranslationGrid.Items[idx.Value]);
        }
    }

    private void PrevMatch_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_vm == null) return;
        var idx = _vm.GetPrevMatchIndex();
        if (idx.HasValue)
        {
            TranslationGrid.SelectedIndex = idx.Value;
            TranslationGrid.ScrollIntoView(TranslationGrid.Items[idx.Value]);
        }
    }
}
