using System;
using System.Reflection;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;

class Program
{
    static void Main()
    {
        var type = typeof(RowSeries<long>);
        var eventInfo = type.GetEvent("PointMeasured");
        Console.WriteLine("Event Handler Type: " + eventInfo.EventHandlerType);
    }
}
