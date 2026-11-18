using System.Windows.Controls;
using BdmSupervisor.Services;
using BdmSupervisor.Xfs;

namespace BdmSupervisor;

/// <summary>Pestañas por módulo (según appsettings "Modules") y pestaña de parámetros del SP.</summary>
public partial class MainWindow
{
    private ParamsTab? _paramsTab;
    private bool _paramsLoaded;

    private void InitModules()
    {
        XfsModules.SiuPinNames = _cfg.SiuPinNames;
        int at = Tabs.Items.IndexOf(MaintTab);
        foreach (var m in _cfg.Modules)
        {
            var tab = new TabItem { Header = m.Title, Content = new ModuleTab(m, _cfg, _log) };
            Tabs.Items.Insert(at++, tab);
        }
        _paramsTab = new ParamsTab(_params, _cfg, _log, () => _dev?.Description ?? "");
        Tabs.Items.Insert(at, new TabItem { Header = "Parámetros SP", Content = _paramsTab });
        _log.Info($"Equipo {_cfg.Model}: módulos " + string.Join(", ", _cfg.Modules.Select(m => $"{m.Title} ({m.Type}:{m.LogicalName})")));

        Tabs.SelectionChanged += async (s, e) =>
        {
            if (e.Source != Tabs || Tabs.SelectedItem is not TabItem t) return;
            if (t.Content is ModuleTab mt) await mt.RefreshAsync();
            else if (t.Content is ParamsTab pt && !_paramsLoaded) { _paramsLoaded = true; pt.Reload(); }
        };
    }
}
