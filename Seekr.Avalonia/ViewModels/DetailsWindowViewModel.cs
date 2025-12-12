using System.Collections.Generic;
using Seekr.Models;

namespace Seekr.Avalonia.ViewModels;

public class DetailsWindowViewModel
{
    public string Title { get; }
    public List<FileSystemNode> Items { get; }

    public DetailsWindowViewModel(string title, List<FileSystemNode> items)
    {
        Title = title;
        Items = items;
    }
}
