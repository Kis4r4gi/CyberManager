using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CyberManager.Shared;

namespace CyberManager.Desktop.Services;

/// <summary>
/// Recorre la interfaz y traduce los textos estáticos. Las propiedades enlazadas a datos
/// se dejan intactas para no alterar la información que viene del servidor.
/// </summary>
public static class Loc
{
    public static void Apply(DependencyObject? root)
    {
        if (root is null) return;

        ApplyOne(root);

        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject element) Apply(element);
        }
    }

    private static void ApplyOne(DependencyObject element)
    {
        switch (element)
        {
            case Window window:
                window.Title = L.T(window.Title);
                break;

            case TextBlock textBlock:
                if (!BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty))
                    textBlock.Text = L.T(textBlock.Text);
                break;

            case DataGrid grid:
                foreach (var column in grid.Columns)
                    if (column.Header is string gridHeader) column.Header = L.T(gridHeader);
                break;

            case HeaderedContentControl headered:
                if (headered.Header is string contentHeader &&
                    !BindingOperations.IsDataBound(headered, HeaderedContentControl.HeaderProperty))
                    headered.Header = L.T(contentHeader);
                break;

            case HeaderedItemsControl items:
                if (items.Header is string itemsHeader &&
                    !BindingOperations.IsDataBound(items, HeaderedItemsControl.HeaderProperty))
                    items.Header = L.T(itemsHeader);
                break;

            case ContentControl content:
                if (content.Content is string contentText &&
                    !BindingOperations.IsDataBound(content, ContentControl.ContentProperty))
                    content.Content = L.T(contentText);
                break;

            case ComboBox combo:
                TranslateStringItems(combo.Items);
                break;

            case ListBox list:
                TranslateStringItems(list.Items);
                break;
        }

        if (element is FrameworkElement { ToolTip: string tooltip } fe &&
            !BindingOperations.IsDataBound(fe, FrameworkElement.ToolTipProperty))
            fe.ToolTip = L.T(tooltip);
    }

    private static void TranslateStringItems(ItemCollection items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is string text) items[i] = L.T(text);
        }
    }
}
