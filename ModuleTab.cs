using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BdmSupervisor.Services;
using BdmSupervisor.Xfs;

namespace BdmSupervisor;

/// <summary>
/// Pestaña genérica de un módulo XFS (impresora, lectora, pinpad, SIU, cámaras, código de barras...):
/// barra de estado verde/roja, detalle del estado y una prueba propia del tipo de módulo.
/// Se arma por código a partir de la lista "Modules" de appsettings.json.
/// </summary>
public sealed class ModuleTab : Grid
{
    private readonly ModuleDef _m;
    private readonly AppConfig _cfg;
    private readonly LogService _log;
    private readonly Border _bar;
    private readonly TextBlock _barTitle, _barDetail, _sp, _extra, _result;
    private readonly StackPanel _lines;
    private readonly StackPanel? _lights;   // solo SIU: una fila por luz guía
    private readonly Button _refresh, _test;
    private readonly Image _img;
    private bool _busy;

    public ModuleDef Def => _m;

    static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    public ModuleTab(ModuleDef m, AppConfig cfg, LogService log)
    {
        _m = m; _cfg = cfg; _log = log;
        Margin = new Thickness(24, 16, 24, 16);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Barra de estado
        _barTitle = new TextBlock { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Text = m.Title.ToUpperInvariant() };
        _barDetail = new TextBlock { FontSize = 14, Foreground = Brushes.White, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap, Text = "Leyendo estado..." };
        _bar = new Border { Background = Res("Muted"), CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 12, 18, 12), Margin = new Thickness(0, 0, 0, 16),
            Child = new StackPanel { Children = { _barTitle, _barDetail } } };
        SetColumnSpan(_bar, 3);
        Children.Add(_bar);

        // Izquierda: estado
        _lines = new StackPanel();
        _sp = new TextBlock { FontSize = 12, Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        _extra = new TextBlock { FontSize = 12, Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        _refresh = new Button { Content = "ACTUALIZAR ESTADO", HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 240, Margin = new Thickness(0, 14, 0, 0) };
        _refresh.Click += async (_, _) => await RefreshAsync();
        var left = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Estado", Style = (Style)Application.Current.Resources["H1"] },
                new TextBlock { Text = $"Servicio lógico XFS: {m.LogicalName}", FontSize = 13, Foreground = Res("Muted"), Margin = new Thickness(0, 0, 0, 10) },
                _lines, _sp, _extra, _refresh,
            }
        };
        var leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = left };
        SetRow(leftScroll, 1);
        Children.Add(leftScroll);

        // Derecha: prueba
        string caption = XfsModules.TestCaption(m.Type);
        _test = new Button { Content = caption, Style = (Style)Application.Current.Resources["Primary"], HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 320,
            Visibility = caption.Length > 0 ? Visibility.Visible : Visibility.Collapsed };
        _test.Click += async (_, _) => await TestAsync();
        _result = new TextBlock { FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 12, 0, 0), FontFamily = new FontFamily("Segoe UI") };
        _img = new Image { MaxHeight = 360, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6, 12, 0, 0), Visibility = Visibility.Collapsed };
        bool siu = m.Type.Equals("SIU", StringComparison.OrdinalIgnoreCase);
        if (siu) _lights = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        var right = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = siu ? "Indicadores luminosos" : "Prueba", Style = (Style)Application.Current.Resources["H1"] },
                new TextBlock { Text = XfsModules.TestHelp(m.Type), FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) },
            }
        };
        if (_lights is not null) right.Children.Add(_lights);
        right.Children.Add(_test); right.Children.Add(_result); right.Children.Add(_img);
        var rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = right };
        SetRow(rightScroll, 1); SetColumn(rightScroll, 2);
        Children.Add(rightScroll);
    }

    public async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true; _refresh.IsEnabled = _test.IsEnabled = false;
        _barDetail.Text = "Leyendo estado...";
        try
        {
            var st = await Task.Run(() => XfsModules.ReadStatus(_m, _cfg, _log));
            Show(st);
        }
        catch (Exception ex) { _barDetail.Text = "Error: " + ex.Message; _bar.Background = Res("Bad"); }
        finally { _busy = false; _refresh.IsEnabled = _test.IsEnabled = true; }
    }

    private void Show(ModuleStatus st)
    {
        _lines.Children.Clear();
        if (!st.Opened)
        {
            _bar.Background = Res("Bad");
            _barDetail.Text = $"SIN DISPOSITIVO / NO RESPONDE · {st.Error}";
            _sp.Text = ""; _extra.Text = "";
            AddLine(new ModuleLine("Servicio XFS", "No se pudo abrir (" + st.Error + ")", false));
            ShowLights(st);
            _log.Info($"[{_m.LogicalName}] no se pudo abrir: {st.Error}");
            Loc.Attach(_lines);
            return;
        }
        bool allOk = st.Ok && st.Lines.All(l => l.Ok != false);
        _bar.Background = Res(allOk ? "Ok" : st.Ok ? "Warn" : "Bad");
        _barDetail.Text = st.Error.Length > 0 ? "Error al leer el estado: " + st.Error
            : allOk ? "EN LÍNEA · SIN ALERTAS" : XfsModules.DeviceName(st.Device) + (st.Ok ? " · VERIFICAR" : "");
        foreach (var l in st.Lines) AddLine(l);
        ShowLights(st);
        _sp.Text = st.Sp.Length > 0 ? "Service Provider: " + st.Sp : "";
        _extra.Text = st.Extra.Length > 0 ? "Información adicional del SP: " + st.Extra : "";
        _log.Info($"[{_m.LogicalName}] estado: " + string.Join(" | ", st.Lines.Select(l => $"{l.Label}={l.Value}")) + (st.Extra.Length > 0 ? " | " + st.Extra : ""));
        Loc.Attach(_lines);
    }

    private void ShowLights(ModuleStatus st)
    {
        if (_lights is null) return;
        _lights.Children.Clear();
        if (st.Lights.Count == 0)
        {
            _lights.Children.Add(new TextBlock { Text = st.Opened ? "La SIU no informa luces guía." : "Sin conexión con la SIU.", FontSize = 14, Foreground = Res("Muted") });
        }
        foreach (var (index, name, value) in st.Lights)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var n = new TextBlock { Text = name, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            bool lit = value is 0x04 or 0x08 or 0x10 or 0x80;
            var v = new TextBlock { Text = XfsModules.SiuLight(value), FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                Foreground = lit ? Res("Warn") : Res("Muted") };
            SetColumn(v, 1);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (caption, cmd) in new[] { ("ENCENDER", XfsModules.LightOn), ("TITILAR", XfsModules.LightFlash), ("APAGAR", XfsModules.LightOff) })
            {
                var b = new Button { Content = caption, MinWidth = 96, MinHeight = 34, FontSize = 13, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(4, 0, 0, 0) };
                int light = index, command = cmd;
                b.Click += async (_, _) => await LightAsync(light, command);
                buttons.Children.Add(b);
            }
            SetColumn(buttons, 2);
            g.Children.Add(n); g.Children.Add(v); g.Children.Add(buttons);
            _lights.Children.Add(g);
        }
        Loc.Attach(_lights);
    }

    private async Task LightAsync(int light, int command)
    {
        if (_busy) return;
        _busy = true; _refresh.IsEnabled = _test.IsEnabled = _lights!.IsEnabled = false;
        try
        {
            var r = await Task.Run(() => XfsModules.SetGuideLight(_m, _cfg, _log, light, command));
            _result.Text = r.Text;
            _result.Foreground = Res(r.Ok ? "Ok" : "Bad");
            _log.Info($"[{_m.LogicalName}] luz {light} comando 0x{command:X}: {r.Text}");
        }
        catch (XfsException ex) { _result.Text = "No se pudo abrir el servicio: " + XfsConst.ErrorName(ex.HResult2); _result.Foreground = Res("Bad"); }
        catch (Exception ex) { _result.Text = "Error: " + ex.Message; _result.Foreground = Res("Bad"); }
        finally { _busy = false; _refresh.IsEnabled = _test.IsEnabled = _lights.IsEnabled = true; }
        await Task.Delay(400);   // la SIU de SNBC informa el estado nuevo con un pequeño retraso
        await RefreshAsync();
    }

    private void AddLine(ModuleLine l)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var name = new TextBlock { Text = l.Label, FontSize = 15, Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap };
        var val = new TextBlock { Text = l.Value, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            Foreground = l.Ok == false ? Res("Bad") : l.Ok == true ? Res("Ok") : Res("Dark") };
        SetColumn(val, 1);
        g.Children.Add(name); g.Children.Add(val);
        _lines.Children.Add(g);
    }

    private async Task TestAsync()
    {
        if (_busy) return;
        _busy = true; _refresh.IsEnabled = _test.IsEnabled = false;
        _img.Visibility = Visibility.Collapsed;
        _result.Foreground = Res("Dark");
        _result.Text = _m.Type.ToUpperInvariant() switch
        {
            "IDC" => "Pase o inserte la tarjeta…",
            "NFC" => "Acerque la tarjeta al lector…",
            "PIN" => "Presione teclas en el pinpad y termine con ENTER…",
            "BCR" => "Acerque el código al lector…",
            _ => "Probando…",
        };
        _log.Info($"[{_m.LogicalName}] prueba: {XfsModules.TestCaption(_m.Type)}");
        try
        {
            var r = await Task.Run(() => XfsModules.Test(_m, _cfg, _log));
            _result.Text = r.Text;
            _result.Foreground = Res(r.Ok ? "Ok" : "Bad");
            _log.Info($"[{_m.LogicalName}] resultado: {r.Text.Replace('\n', ' ')}");
            if (r.ImagePath is not null && File.Exists(r.ImagePath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.UriSource = new Uri(r.ImagePath); bmp.EndInit();
                _img.Source = bmp; _img.Visibility = Visibility.Visible;
            }
        }
        catch (XfsException ex)
        {
            _result.Text = "No se pudo abrir el servicio: " + XfsConst.ErrorName(ex.HResult2);
            _result.Foreground = Res("Bad");
        }
        catch (Exception ex) { _result.Text = "Error: " + ex.Message; _result.Foreground = Res("Bad"); }
        finally { _busy = false; _refresh.IsEnabled = _test.IsEnabled = true; }
        await RefreshAsync();
    }
}
