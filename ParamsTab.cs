using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using BdmSupervisor.Services;

namespace BdmSupervisor;

/// <summary>Fila de la grilla de parámetros: textos traducidos y valor editable.</summary>
public sealed class ParamRow(SpParam p)
{
    public SpParam P { get; } = p;
    public string Group => Loc.T(P.Group);
    public string Name => Loc.T(P.Name);
    public string Description => Loc.T(P.Description);
    public string Location => P.Location + (P.Section.Length > 0 ? " " + P.Key : "");
    public string Value { get => P.Value; set => P.Value = value.Trim(); }
    public string Mark => P.Changed ? "●" : "";
}

/// <summary>
/// Pestaña "Parámetros del SP": reporte de la configuración del Service Provider de SNBC
/// y edición protegida con contraseña (velocidad, reintentos, moneda, shutters, SIU...).
/// </summary>
public sealed class ParamsTab : Grid
{
    private readonly SpParamsService _svc;
    private readonly AppConfig _cfg;
    private readonly LogService _log;
    private readonly Func<string> _spDescription;
    private readonly DataGrid _grid;
    private readonly TextBlock _header, _lockText, _note;
    private readonly Button _unlock, _save, _export, _reload;
    private List<ParamRow> _rows = [];
    private bool _unlocked;
    private readonly string[] _headers = ["", "Grupo", "Parámetro", "Valor", "Archivo / clave", "Descripción"];

    static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    public ParamsTab(SpParamsService svc, AppConfig cfg, LogService log, Func<string> spDescription)
    {
        _svc = svc; _cfg = cfg; _log = log; _spDescription = spDescription;
        Margin = new Thickness(24, 16, 24, 16);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _header = new TextBlock { FontSize = 13, Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        Children.Add(new StackPanel { Children = { new TextBlock { Text = "Parámetros del Service Provider XFS", Style = (Style)Application.Current.Resources["H1"] }, _header } });

        _lockText = new TextBlock { FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), TextWrapping = TextWrapping.Wrap };
        _unlock = new Button { Content = "DESBLOQUEAR EDICIÓN", MinWidth = 240 };
        _unlock.Click += (_, _) => ToggleLock();
        _save = new Button { Content = "GUARDAR CAMBIOS", Style = (Style)Application.Current.Resources["Primary"], MinWidth = 220, IsEnabled = false };
        _save.Click += (_, _) => Save();
        _reload = new Button { Content = "VOLVER A LEER", MinWidth = 180 };
        _reload.Click += (_, _) => Reload();
        _export = new Button { Content = "EXPORTAR REPORTE", MinWidth = 200 };
        _export.Click += (_, _) => Export();
        var bar = new WrapPanel { Margin = new Thickness(-6, 0, 0, 8), Children = { _unlock, _save, _reload, _export, _lockText } };
        SetRow(bar, 1); Children.Add(bar);

        _grid = new DataGrid
        {
            AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
            FontSize = 14, RowHeight = 30, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE8)),
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD6)), SelectionUnit = DataGridSelectionUnit.Cell,
        };
        _grid.Columns.Add(new DataGridTextColumn { Binding = new Binding(nameof(ParamRow.Mark)), IsReadOnly = true, Width = 22, Foreground = Res("Brand") });
        _grid.Columns.Add(new DataGridTextColumn { Binding = new Binding(nameof(ParamRow.Group)), IsReadOnly = true, Width = 130 });
        _grid.Columns.Add(new DataGridTextColumn { Binding = new Binding(nameof(ParamRow.Name)), IsReadOnly = true, Width = 300 });
        var valStyle = new Style(typeof(TextBlock));
        valStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        _grid.Columns.Add(new DataGridTextColumn { Binding = new Binding(nameof(ParamRow.Value)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = 120, ElementStyle = valStyle });
        _grid.Columns.Add(new DataGridTextColumn { Binding = new Binding(nameof(ParamRow.Location)), IsReadOnly = true, Width = 260 });
        _grid.Columns.Add(new DataGridTextColumn { Binding = new Binding(nameof(ParamRow.Description)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.BeginningEdit += (_, e) => { if (!_unlocked || e.Row.Item is not ParamRow r || !r.P.Editable) e.Cancel = true; };
        _grid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(() => { _grid.Items.Refresh(); UpdateButtons(); });
        SetRow(_grid, 2); Children.Add(_grid);

        _note = new TextBlock { FontSize = 13, Foreground = Res("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            Text = "Antes de guardar se hace una copia de cada archivo (.bak-fecha). Los cambios los toma el Service Provider al reiniciar el equipo." };
        SetRow(_note, 3); Children.Add(_note);

        Loc.Changed += () => { ApplyHeaders(); _grid.Items.Refresh(); };
        ApplyHeaders();
        UpdateLockText();
    }

    private void ApplyHeaders()
    {
        for (int i = 0; i < _grid.Columns.Count; i++) _grid.Columns[i].Header = Loc.T(_headers[i]);
    }

    /// <summary>Lee los parámetros del SP (al entrar a la pestaña).</summary>
    public void Reload()
    {
        if (_rows.Any(r => r.P.Changed) &&
            Mb.Show("Hay cambios sin guardar. ¿Descartarlos y volver a leer?", "Parámetros del SP", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            _rows = _svc.Load().Select(p => new ParamRow(p)).ToList();
            _grid.ItemsSource = _rows;
            _header.Text = (_svc.SpInstalled ? "Service Provider: " : "No se encontró el SP en ") + (_svc.SpInstalled ? SpInfo() : SpParamsService.SpDir);
        }
        catch (Exception ex) { _header.Text = "Error al leer los parámetros: " + ex.Message; }
        UpdateButtons();
    }

    private string SpInfo()
    {
        var parts = new List<string>();
        string d = _spDescription(); if (d.Length > 0) parts.Add(d);
        foreach (var (label, file) in new[] { ("BNEHostDll", @"SnbcTCR\BNEHostDll.dll"), ("SNBC_SPCRM", @"SnbcTCR\SNBC_SPCRM.exe") })
        {
            string f = Path.Combine(SpParamsService.SpDir, file);
            if (File.Exists(f)) parts.Add($"{label} {FileVersionInfo.GetVersionInfo(f).FileVersion}");
        }
        parts.Add(SpParamsService.SpDir);
        return string.Join(" · ", parts);
    }

    private void ToggleLock()
    {
        if (_unlocked)
        {
            _unlocked = false;
        }
        else
        {
            string? pwd = PasswordDialog.Ask(Window.GetWindow(this), "Contraseña para modificar los parámetros del SP");
            if (pwd is null) return;
            if (pwd != _cfg.ParamsPassword)
            {
                _log.Info("Parámetros del SP: contraseña incorrecta");
                Mb.Show("Contraseña incorrecta.", "Parámetros del SP", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _unlocked = true;
            _log.Info("Parámetros del SP: edición desbloqueada");
        }
        UpdateLockText();
        UpdateButtons();
    }

    private void UpdateLockText()
    {
        _unlock.Content = _unlocked ? "BLOQUEAR EDICIÓN" : "DESBLOQUEAR EDICIÓN";
        _lockText.Text = _unlocked ? "Edición habilitada: doble clic en la columna Valor para cambiarlo." : "Solo lectura. Para modificar valores desbloquee la edición con la contraseña.";
        _lockText.Foreground = _unlocked ? Res("Bad") : Res("Muted");
    }

    private void UpdateButtons() => _save.IsEnabled = _unlocked && _rows.Any(r => r.P.Changed);

    /// <summary>Valida el valor según el tipo de parámetro. Devuelve el error o null.</summary>
    private static string? Validate(SpParam p)
    {
        string v = p.Value;
        bool isInt = int.TryParse(v, out int n);
        switch (p.Key)
        {
            case "mSortPPM" or "mCountPPM" or "mDenomCountPPM":
                return isInt && n is >= 300 and <= 1200 ? null : "la velocidad debe ser un número entre 300 y 1200";
            case "retry" or "successretry":
                return isInt && n is >= 0 and <= 10 ? null : "los reintentos deben ser un número entre 0 y 10";
            case "CurrencyName":
                return Regex.IsMatch(v, "^[A-Z]{3}$") ? null : "la moneda debe ser un código ISO de 3 letras (ARS, USD...)";
            case "MaxCashInItems" or "IntermediateStacker" or "NoACKEnterVDMTime" or "BoardCount":
                return isInt && n >= 0 ? null : "debe ser un número entero";
        }
        if (p.Original is "0" or "1") return v is "0" or "1" ? null : "debe ser 0 o 1";
        if (p.IsDword) return isInt ? null : "debe ser un número entero";
        return v.Length > 0 ? null : "no puede quedar vacío";
    }

    private void Save()
    {
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
        var changed = _rows.Where(r => r.P.Changed && r.P.Editable).ToList();
        if (changed.Count == 0) return;
        foreach (var r in changed)
        {
            string? err = Validate(r.P);
            if (err is not null) { Mb.Show($"{r.Name}: {err}", "Parámetros del SP", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        }
        string list = string.Join("\n", changed.Select(r => $"• {r.Name}: {r.P.Original} → {r.P.Value}"));
        if (Mb.Show("Se van a guardar estos cambios en la configuración del Service Provider:\n\n" + list + "\n\n¿Continuar?", "Parámetros del SP",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var done = _svc.Save(changed.Select(r => r.P));
            _grid.Items.Refresh();
            Mb.Show("Cambios guardados:\n\n" + string.Join("\n", done) + "\n\nLos cambios los toma el Service Provider al reiniciar el equipo.",
                "Parámetros del SP", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _log.Error("Parámetros del SP: error al guardar: " + ex.Message);
            Mb.Show("Error al guardar: " + ex.Message, "Parámetros del SP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        UpdateButtons();
    }

    private void Export()
    {
        try
        {
            Directory.CreateDirectory(_cfg.ExportDir);
            string file = Path.Combine(_cfg.ExportDir, $"Parametros_SP_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine(Loc.T("Reporte de parámetros del Service Provider XFS"));
            sb.AppendLine($"{Environment.MachineName} · {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine(SpInfo());
            sb.AppendLine();
            foreach (var g in _rows.GroupBy(r => r.Group))
            {
                sb.AppendLine($"[{g.Key}]");
                foreach (var r in g) sb.AppendLine($"  {r.Name,-48} {r.Value,-10} {r.Location}");
                sb.AppendLine();
            }
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
            _log.Info("Reporte de parámetros exportado: " + file);
            Process.Start("explorer.exe", $"/select,\"{file}\"");
        }
        catch (Exception ex) { Mb.Show("Error al exportar: " + ex.Message, "Parámetros del SP"); }
    }
}

/// <summary>Diálogo simple para pedir una contraseña.</summary>
public static class PasswordDialog
{
    public static string? Ask(Window? owner, string prompt)
    {
        var box = new PasswordBox { FontSize = 18, Margin = new Thickness(0, 10, 0, 14), Padding = new Thickness(6) };
        var ok = new Button { Content = Loc.T("ACEPTAR"), IsDefault = true, MinWidth = 140, MinHeight = 44, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = Loc.T("CANCELAR"), IsCancel = true, MinWidth = 140, MinHeight = 44, Margin = new Thickness(0) };
        var w = new Window
        {
            Title = Loc.T("Parámetros del SP"), Width = 440, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner, Owner = owner, FontFamily = new FontFamily("Segoe UI"),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children = { new TextBlock { Text = Loc.T(prompt), FontSize = 15, TextWrapping = TextWrapping.Wrap }, box,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } } }
            }
        };
        ok.Click += (_, _) => w.DialogResult = true;
        w.Loaded += (_, _) => box.Focus();
        return w.ShowDialog() == true ? box.Password : null;
    }
}
