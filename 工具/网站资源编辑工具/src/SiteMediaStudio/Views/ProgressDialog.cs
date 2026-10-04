using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PersonalWorks.SiteMediaStudio.Core;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 命令进行中的进度窗：百分比、当前步、最近一行输出。不可取消。执行与发布共用。
/// </summary>
public sealed class ProgressDialog : Window
{
    private readonly TextBlock mStepText;
    private readonly TextBlock mPercentText;
    private readonly TextBlock mDetailText;
    private readonly TextBlock mRetryText;
    private readonly ColumnDefinition mFillColumn;
    private readonly ColumnDefinition mRestColumn;
    private bool mAllowClose;

    private ProgressDialog(string title)
    {
        Title = title;
        Width = 520;
        MinHeight = 220;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushCache.Freeze("#14181C");
        Foreground = BrushCache.Text;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;

        var root = new StackPanel { Margin = new Thickness(20) };
        mStepText = new TextBlock
        {
            Text = "准备中…",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(mStepText);

        var barRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var track = new Grid
        {
            Height = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        mFillColumn = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
        mRestColumn = new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) };
        track.ColumnDefinitions.Add(mFillColumn);
        track.ColumnDefinitions.Add(mRestColumn);
        var well = new Rectangle
        {
            Fill = BrushCache.Freeze("#252A31"),
            RadiusX = 2,
            RadiusY = 2
        };
        Grid.SetColumnSpan(well, 2);
        track.Children.Add(well);
        var fill = new Rectangle
        {
            Fill = BrushCache.Pine,
            RadiusX = 2,
            RadiusY = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(fill, 0);
        track.Children.Add(fill);
        Grid.SetColumn(track, 0);
        barRow.Children.Add(track);

        mPercentText = new TextBlock
        {
            Text = "0%",
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, 微软雅黑"),
            FontSize = 13
        };
        Grid.SetColumn(mPercentText, 1);
        barRow.Children.Add(mPercentText);
        root.Children.Add(barRow);

        mDetailText = new TextBlock
        {
            Text = "请稍候。",
            Margin = new Thickness(0, 12, 0, 0),
            Foreground = BrushCache.Muted,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        };
        root.Children.Add(mDetailText);
        mRetryText = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = BrushCache.Muted,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Visibility = Visibility.Collapsed
        };
        root.Children.Add(mRetryText);
        Content = root;
        Closing += (_, e) =>
        {
            if (!mAllowClose)
            {
                e.Cancel = true;
            }
        };
    }

    /// <summary>
    /// 后台执行并显示进度；窗体关闭后返回结果。
    /// </summary>
    public static BatchRunResult Run(
        string title,
        Func<IProgress<PublishProgress>, BatchRunResult> work)
    {
        var dialog = new ProgressDialog(title)
        {
            Owner = Application.Current?.MainWindow
        };
        var progress = new Progress<PublishProgress>(dialog.Apply);
        var started = false;
        dialog.ContentRendered += async (_, _) =>
        {
            if (started)
            {
                return;
            }

            started = true;
            try
            {
                dialog.Tag = await Task.Run(() => work(progress)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                dialog.Tag = new BatchRunResult
                {
                    Ok = false,
                    FailedStep = title,
                    FailureText = ex.Message
                };
            }
            finally
            {
                dialog.mAllowClose = true;
                dialog.DialogResult = true;
            }
        };
        dialog.ShowDialog();
        WindowActivation.BringToFront(dialog.Owner);
        return dialog.Tag as BatchRunResult
            ?? new BatchRunResult
            {
                Ok = false,
                FailedStep = title,
                FailureText = title + "未返回结果。"
            };
    }

    /// <summary>
    /// 把一条进度快照画到窗口上。
    /// </summary>
    private void Apply(PublishProgress progress)
    {
        mStepText.Text = $"第 {progress.CurrentStep} / {progress.TotalStep} 步 · {progress.Title}";
        mPercentText.Text = progress.Percent + "%";
        var fill = Math.Max(progress.Percent, 0);
        var remain = Math.Max(0, 100 - fill);
        mFillColumn.Width = new GridLength(fill, GridUnitType.Star);
        mRestColumn.Width = new GridLength(remain == 0 && fill == 0 ? 1 : remain, GridUnitType.Star);
        mDetailText.Text = string.IsNullOrWhiteSpace(progress.Detail)
            ? "进行中…"
            : progress.Detail;
        if (string.IsNullOrWhiteSpace(progress.RetrySummary))
        {
            mRetryText.Text = "";
            mRetryText.Visibility = Visibility.Collapsed;
        }
        else
        {
            mRetryText.Text = progress.RetrySummary;
            mRetryText.Visibility = Visibility.Visible;
        }
    }
}
