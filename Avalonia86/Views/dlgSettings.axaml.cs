using Avalonia86.Core;
using Avalonia86.Tools;
using Avalonia86.Xplat;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Styling;
using Avalonia;
using Avalonia86.DialogBox;
using static Avalonia86.Views.dlgSettingsModel;

namespace Avalonia86.Views;

public partial class dlgSettings : BaseWindow
{
    private readonly dlgSettingsModel _m;
    private bool _cancel_or_ok;

    public dlgSettings() : base("settings")
    {
        InitializeComponent();
        BaseInit();
        Closing += dlgSettings_FormClosing;
        Closed += dlgSettings_Closed;

        _m = new dlgSettingsModel();
        DataContext = _m;

        _m.PropertyChanged += _m_PropertyChanged;
    }


    private void label_default_tapped(object sender, TappedEventArgs args)
    {
        //Wasn't able to get Label's "target" function to work. Tried both using x:Name and Elementname=
        _m.IsDefChecked = true;
    }

    private void dlgSettings_Closed(object sender, EventArgs e)
    {
        _m.Dispose();
    }

    private void _m_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(dlgSettingsModel.ExeDir))
        {
            Get86BoxVersion();
        }
    }

    private async void dlgSettings_Load(object sender, EventArgs e)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif

        try
        {
            LoadSettings();
            Get86BoxVersion();
        }
        catch (Exception ex)
        {
            await this.ShowError("Settings could not be loaded, sorry.", ex, "Failure");

            Close();
        }
    }

    private async void dlgSettings_FormClosing(object sender, WindowClosingEventArgs e)
    {
        if (!_m.HasChanges || _cancel_or_ok)
            return;

        e.Cancel = true;

        // Unsaved changes, ask the user to confirm
        var result = await this.ShowQuestion(
            "Would you like to save the changes you've made to the settings?", 
            "Unsaved changes");
        if (result == DialogResult.Yes)
        {
            await SaveSettings();
        }

        if (result != DialogResult.None)
        {
            Closing -= dlgSettings_FormClosing;
            Close();
        }
    }

    private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        // We want to make the first row (index 0) not editable
        if (e.Row.Index == 0)
        {
            //e.Cancel = true;
        }
    }

    private async void btnApply_Click(object sender, RoutedEventArgs e)
    {
        var success = await SaveSettings();
    }

    private async void btnOK_Click(object sender, RoutedEventArgs e)
    {
        if (_m.HasChanges)
        {
            await SaveSettings();
        }
        _cancel_or_ok = true;
        Close(DialogResult.Ok);
    }

    private async void btnBrowse3_Click(object sender, RoutedEventArgs e)
    {
        var dir = Platforms.Env.MyComputer;
        var title = "Select a file where 86Box logs will be saved";
        var filter = "Log files (*.log)|*.log";
        string fileName = null;

        try
        {
            fileName = await Dialogs.SaveFile(title, dir, filter, parent: this, ext: ".log");
        }
        catch (Exception ex)
        {
            await this.ShowError("Failed to open file dialog.", ex);
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _m.LogPath = fileName;
        }
    }

    private async void btnDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = await this.ShowQuestion("All settings will be reset to their default values. " +
                                             "Do you wish to continue?", "Settings will be reset");
        if (result == DialogResult.Yes)
        {
            ResetSettings();
        }
    }

    // Resets the settings to their default values
    private void ResetSettings()
    {
        var (cfgPath, exePath) = VMCenter.FindPaths();
        _m.CFGDir = cfgPath;
        _m.ExeDir = exePath;
        _m.ROMDir = null;
        _m.MinOnStart = false;
        _m.EnableConsole = true;
        _m.IsTrayEnabled = false;
        _m.MinToTray = false;
        _m.CloseToTray = false;
        _m.EnableLogging = false;
        _m.LogPath = "";
        _m.AllowInstances = false;
        _m.CompactList = false;
        _m.RenameFolders = true;
        _m.SelectedTheme = ThemeVariant.Default;
        _m.ToolBar86Settings = true;
        _m.ToolBarPSSettings = false;
        _m.IsDefChecked = true;
        _m.IsExeListChanged = false;
        _m.RaisePropertyChanged(nameof(dlgSettingsModel.HasChanges));
    }

    private async void btnBrowse1_Click(object sender, RoutedEventArgs e)
    {
        var text = "Select a folder where 86Box program files and the roms folder are located";

        var fldName = await Dialogs.SelectFolder(_m.ExeDir, text, parent: this);

        if (!string.IsNullOrWhiteSpace(fldName))
        {
            _m.ExeDir = fldName.CheckTrail();
        }
    }

    private async void btnBrowse2_Click(object sender, RoutedEventArgs e)
    {
        var text = "Select a folder where your virtual machines (configs, nvr folders, etc.) will be located";
        var dir = _m.CFGDir;
        if (string.IsNullOrWhiteSpace(dir))
        {
            var exe = _m.ExeDir;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                try { dir = Path.GetDirectoryName(exe); } catch { }
            }
        }

        var fileName = await Dialogs.SelectFolder(dir, text, parent: this);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _m.CFGDir = fileName.CheckTrail();
        }
    }

    private async void btnBrowse_rom_Click(object sender, RoutedEventArgs e)
    {
        var text = "Select the folder where 86Box can find firmware and bios files";
        var dir = _m.ROMDir;
        if (string.IsNullOrWhiteSpace(dir))
        {
            var exe = _m.ExeDir;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                try { dir = Path.GetDirectoryName(exe); } catch { }
            }
        }

        var fileName = await Dialogs.SelectFolder(dir, text, parent: this);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _m.ROMDir = fileName.CheckTrail();
        }
    }

    /// <summary>
    /// Obtains the 86Box version from 86Box executable
    /// </summary>
    private void Get86BoxVersion()
    {
        _m.ExeValid = false;
        _m.ExeError = false;
        _m.ExeWarn = false;
        _m.ExePath = "86Box executable not found!";

        try
        {
            var files = Platforms.Manager.List86BoxExecutables(_m.ExeDir);
            if (files != null)
            {
                foreach (var file in files)
                {
                    var vi = Platforms.Manager.Get86BoxInfo(file, out _);
                    if (vi != null)
                    {
                        if (vi.FilePrivatePart >= 3541) //Officially supported builds
                        {
                            _m.ExePath = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart} - fully compatible";
                            _m.ExeValid = true;
                        }
                        else if (vi.FilePrivatePart >= 3333) //Should mostly work...
                        {
                            _m.ExePath = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart} - partially compatible";
                            _m.ExeWarn = true;
                        }
                        else if (vi.FilePrivatePart >= 2000) //Should work...
                        {
                            _m.ExePath = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart} - limited compatibility";
                            _m.ExeWarn = true;
                        }
                        else //Completely unsupported, since version info can't be obtained anyway
                        {
                            _m.ExePath = "Unknown - may not be compatible";
                            _m.ExeError = true;
                        }

                        break;
                    }  
                }
            }
        }
        catch { }

        _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeError));
        _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeWarn));
        _m.RaisePropertyChanged(nameof(dlgSettingsModel.ExeValid));
    }

    // Save the settings to the registry
    private async Task<bool> SaveSettings()
    {
        if (_m.EnableLogging && string.IsNullOrWhiteSpace(_m.LogPath))
        {
            var result = await this.ShowQuestion(
                "Using an empty or whitespace string for the log path will " +
                "prevent 86Box from logging anything. Are you sure you want to use" +
                " this path?");
            if (result == DialogResult.No)
            {
                return false;
            }
        }

        try
        {
            //Store the new values, close the key, changes are saved
            var s = AppSettings.Settings;
            using (var t = s.BeginTransaction())
            {
                string dir = _m.ExeDir;
                if (dir != null)
                    dir = dir.CheckTrail();
                s.EXEdir = dir;
                dir = _m.CFGDir;
                if (string.IsNullOrWhiteSpace(dir))
                    dir = null;
                else if (dir != null)
                    dir = dir.CheckTrail();
                s.CFGdir = dir;
                dir = _m.ROMDir;
                if (string.IsNullOrWhiteSpace(dir))
                    dir = null;
                else if (dir != null)
                    dir = dir.CheckTrail();
                s.ROMdir = dir;
                s.MinimizeOnVMStart = _m.MinOnStart;
                s.ShowConsole = _m.EnableConsole;
                s.IsTrayEnabled = _m.IsTrayEnabled;
                s.MinimizeToTray = _m.MinToTray;
                s.CloseTray = _m.CloseToTray;
                s.EnableLogging = _m.EnableLogging;
                s.RenameFolders = _m.RenameFolders;
                s.LogPath = _m.LogPath;
                s.AllowInstances = _m.AllowInstances;
                s.CompactMachineList = _m.CompactList;
                s.Theme = _m.SelectedTheme;
                s.Has86ToolbarBtn = _m.ToolBar86Settings;
                s.HasPSToolbarBtn = _m.ToolBarPSSettings;

                foreach (var exe in _m.Executables.Items)
                {
                    if (exe.IsDeleted)
                    {
                        if (exe.IsNew)
                            continue;

                        try { s.RemoveExe(exe.ID); }
                        catch { }
                    }
                    else if (exe.IsNew) {
                        try { s.AddExe(exe.Name, exe.VMPath, exe.VMRoms, exe.Comment, exe.Version, exe.Arch, exe.Build, exe.IsDefault); }
                        catch { }
                    }
                    else if(exe.IsChanged)
                    {
                        try { s.UpdateExe(exe.ID, exe.Name, exe.VMPath, exe.VMRoms, exe.Comment, exe.Version, exe.Arch, exe.Build, exe.IsDefault); }
                        catch { }
                    }
                }

                t.Commit();
            }

            //This will set of a chain of events.
            // - MainModel is listening and will notify if CompactMachineList has changed
            // - frmMain is listening to MainModel and will update the list if the CompactList
            //   property have changed.
            _m.NotifyPropertyChange(s);

            _m.Commit();
        }
        catch (Exception ex)
        {
            await this.ShowError("An error has occurred. Please provide the following information" +
                                $" to the developer:\n{ex.Message}\n{ex.StackTrace}", ex);
            return false;
        }
        finally
        {
            Get86BoxVersion(); //Get the new exe version in any case
        }
        return true;
    }

    /// <summary>
    /// Read the settings from the database
    /// </summary>
    private void LoadSettings()
    {
        var s = AppSettings.Settings;

        _m.ExeDir = s.EXEdir;
        _m.CFGDir = s.CFGdir;
        _m.ROMDir = s.ROMdir;
        _m.LogPath = s.LogPath;
        _m.MinOnStart = s.MinimizeOnVMStart;
        _m.EnableConsole = s.ShowConsole;
        _m.RenameFolders = s.RenameFolders;
        _m.IsTrayEnabled = s.IsTrayEnabled;
        _m.MinToTray = s.MinimizeToTray;
        _m.CloseToTray = s.CloseTray;
        _m.EnableLogging = s.EnableLogging;
        _m.AllowInstances = s.AllowInstances;
        _m.CompactList = s.CompactMachineList;
        _m.SelectedTheme = s.Theme;
        _m.ToolBarPSSettings = s.HasPSToolbarBtn;
        _m.ToolBar86Settings = s.Has86ToolbarBtn;

        if (string.IsNullOrWhiteSpace(_m.ExeDir) || string.IsNullOrWhiteSpace(_m.CFGDir))
        {
            var (cfgPath, exePath) = VMCenter.FindPaths();
            if (string.IsNullOrWhiteSpace(_m.ExeDir))
            {
                _m.ExeDir = exePath;
            }
            if (string.IsNullOrWhiteSpace(_m.CFGDir))
            {
                _m.CFGDir = cfgPath;
            }

        }

        bool is_def = true;

        foreach(var r in s.ListExecutables())
        {
#if MSDB
            var exe = new dlgSettingsModel.ExeEntery(((long)r["IsDef"]) == 1, false)
#else
            var exe = new dlgSettingsModel.ExeEntery((bool)r["IsDef"], false)
#endif
            {
                ID = (long) r["ID"],
                Name = r["Name"] as string,
                VMPath = r["VMExe"] as string,
                VMRoms = r["VMRoms"] as string,
                Version = r["Version"] as string,
                Comment = r["Comment"] as string,
                Arch = r["Arch"] as string,
                Build = r["Build"] as string,
            };

            try { exe.ExeExists = File.Exists(exe.VMPath); }
            catch { }

            if (exe.IsDefault)
                is_def = false;

            exe.PropertyChanged += (o, e) => _m.IsExeListChanged = true;

            _m.Executables.AddOrUpdate(exe);
        }

        _m.IsDefChecked = is_def;
        _m.IsExeListChanged = false;

        _m.Commit();
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cancel_or_ok = true;
        Close(DialogResult.Cancel);
    }

    /// <summary>
    /// Todo: This code locks up the UI, and does not highlight the files it imported in any way.
    /// </summary>
    private async void btnImport_click(object sender, RoutedEventArgs e)
    {
        var text = "Select a folder to scan for 86Box executables";
        var DefExePath = _m.CFGDir;

        string path = string.IsNullOrWhiteSpace(DefExePath) ? "" : DefExePath;
        var folder_name = await Dialogs.SelectFolder(path, text, this);

        if (folder_name != null)
        {
            await new DialogBoxBuilder(this).WithMessage("Warning: the UI will be unresponsive while importing.")
                                            .WithIcon(DialogIcon.Warning)
                                            .ShowDialog();
            try
            {
                foreach (var exe in FolderHelper.GetExecutableFiles(folder_name, "86box", Platforms.Manager.IsExecutable))
                {
                    bool add = true;

                    foreach (var ex in _m.FilteredExecutables)
                    {
                        if (ex.VMPath == exe)
                        {
                            add = false;
                            break;
                        }
                    }

                    if (add)
                    {
                        var vi = Platforms.Manager.Get86BoxInfo(exe, out _);
                        if (vi != null)
                        {

                            var ver_str = $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}";

                            _m.Executables.AddOrUpdate(new dlgSettingsModel.ExeEntery(false)
                            {
                                ID = -1 - _m.Executables.Count,
                                VMPath = exe,
                                Name = $"86Box {ver_str} - build {vi.FilePrivatePart}",
                                Version = ver_str,
                                Arch = vi.Arch,
                                Build = "" + vi.FilePrivatePart
                            });

                            _m.IsExeListChanged = true;
                        }
                    }
                }
            } catch { }
        }
    }

    private async void btnAddExe_Click(object sender, RoutedEventArgs e)
    {
        var win = new dlgAddExe()
        {
            DefExePath = _m.ExeDir
        };
        var m = ((dlgAddExeModel)win.DataContext);
        m.Commit();
        await Tools.Dialogs.RunDialog(this, win, async (dr) =>
        {
            if (dr == DialogResult.Ok)
            {
                bool add = true;

                foreach (var exe in _m.FilteredExecutables)
                {
                    if (exe.VMPath == m.ExePath)
                    {
                        var r = await this.ShowQuestion(
                            $"{m.Name} is already in the list under the name \"{exe.Name}\".\n\nDo you wish to add it anyway?", $"{m.Name} is already in the list");
                        add = r == DialogResult.Yes;
                        break;
                    }
                }

                if (add)
                {
                    _m.Executables.AddOrUpdate(new dlgSettingsModel.ExeEntery(false)
                    {
                        ID = -1 - _m.Executables.Count,
                        VMPath = m.ExePath,
                        VMRoms = m.RomDir,
                        Name = m.Name,
                        Version = m.Version,
                        Comment = m.Comment,
                        Arch = m.Arch,
                        Build = m.Build,
                        ExeExists = true
                    });

                    _m.IsExeListChanged = true;
                }
            }
        });
    }

    private void btnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_m.SelectedExe != null) 
        {
            foreach (var selected in DG_Exes.SelectedItems)
            {
                if (selected is ExeEntery exe)
                {
                    //We don't delete as that would mess up the "id" generating algo
                    exe.IsDeleted = true;
                    if (exe.IsDefault)
                        _m.IsDefChecked = true;
                }
            }
            _m.IsExeListChanged = true;

            //Deleted items are filtered away
            _m.Executables.Refresh();
        }
    }

    private async void btnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_m.SelectedExe == null)
            return;
        var sel = _m.SelectedExe;

        var win = new dlgAddExe()
        {
            DefExePath = _m.ExeDir
        };
        var m = ((dlgAddExeModel)win.DataContext);
        m.Arch = sel.Arch;
        m.Build = sel.Build;
        m.Version = sel.Version;
        m.Comment = sel.Comment;
        m.Name = sel.Name;
        m.RomDir = sel.VMRoms;
        m.ExePath = sel.VMPath;
        m.Commit();
        await Tools.Dialogs.RunDialog(this, win, (dr) =>
        {
            if (dr == DialogResult.Ok)
            {
                sel.Arch = m.Arch;
                sel.Build = m.Build;
                sel.Version = m.Version;
                sel.Comment = m.Comment;
                sel.Name = m.Name;
                sel.VMRoms = m.RomDir;
                sel.VMPath = m.ExePath;
                sel.SetChanged();

                _m.IsExeListChanged = true;
            }
        });
    }

    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is dlgSettingsModel.ExeEntery exe)
        {
            if (exe.ExeExists)
                e.Row.Classes.Remove("missing");
            else
                e.Row.Classes.Add("missing");
        }
    }
}

internal class dlgSettingsModel : ReactiveObject, IDisposable
{
    private string _exe_dir, _exe_path, _cfg_dir, _rom_dir;
    private bool _min_start, _min_tray, _close_tray;
    private bool _enable_logging, _enable_tray, _rename_folders;
    private bool _is_default_selected, _is_exe_list_changed;
    private bool _has_86_toolbar, _has_ps_toolbar;
    private string _log_path;
    private bool _allow_instances, _enable_console;
    private bool _compact_list;
    private ExeEntery _sel_exe;
    private ThemeVariant _theme;

    public SourceCache<ExeEntery, long> Executables = new(o => o.ID);
    private readonly ReadOnlyObservableCollection<ExeEntery> _filtered_executables;
    public ReadOnlyObservableCollection<ExeEntery> FilteredExecutables => _filtered_executables;
    readonly IDisposable _exe_sub;

    dlgSettingsModel _me;

    public bool ToolBar86Settings
    {
        get => _has_86_toolbar;
        set
        {
            if (_has_86_toolbar != value)
            {
                this.RaiseAndSetIfChanged(ref _has_86_toolbar, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool ToolBarPSSettings
    {
        get => _has_ps_toolbar;
        set
        {
            if (_has_ps_toolbar != value)
            {
                this.RaiseAndSetIfChanged(ref _has_ps_toolbar, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public ExeEntery SelectedExe 
    { 
        get => _sel_exe;
        set
        {
            if (_sel_exe != value)
            {
                _sel_exe = value;
                this.RaisePropertyChanged(nameof(HasSelectedExe));
            }
        }
    }

    public bool IsDefChecked
    {
        get => _is_default_selected;
        set
        {
            if (value != _is_default_selected)
            {
                this.RaiseAndSetIfChanged(ref _is_default_selected, value);
                IsExeListChanged = true;
            }
        }
    }

    public bool HasSelectedExe => _sel_exe != null;

    public bool ExeValid { get; set; }
    public bool ExeWarn { get; set; }
    public bool ExeError { get; set; }

    public string Version { get => CurrentApp.VersionString; }

    public bool IsExeListChanged
    {
        get => _is_exe_list_changed;
        set
        {
            if (value != _is_exe_list_changed)
            {
                _is_exe_list_changed = value;
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Lisence
    {
        get
        {
            try
            {
                var startupPath = CurrentApp.StartupPath;
                var txt = Path.Combine(startupPath, "Resources", "LICENSE");
                return File.ReadAllText(txt);
            }
            catch { return "MIT Lisence file not loaded."; }
        }
    }

    public string Authors
    {
        get
        {
            try
            {
                var startupPath = CurrentApp.StartupPath;
                var txt = Path.Combine(startupPath, "Resources", "AUTHORS");
                return File.ReadAllText(txt);
            }
            catch { return "Author file not loaded."; }
        }
    }

    public bool HasChanges
    {
        get 
        {
            if (_me == null)
                return false;

            return IsExeListChanged ||
                   _me.ExeDir != ExeDir ||
                   _me.CFGDir != CFGDir ||
                   _me.ROMDir != ROMDir ||
                   _me.IsTrayEnabled != IsTrayEnabled ||
                   _me.MinToTray != MinToTray ||
                   _me.MinOnStart != MinOnStart ||
                   _me.EnableLogging != EnableLogging ||
                   _me.EnableConsole != EnableConsole ||
                   _me.AllowInstances != AllowInstances ||
                   _me.LogPath != LogPath ||
                   _me.CompactList != CompactList ||
                   _me.RenameFolders != RenameFolders ||
                   _me.ToolBar86Settings != ToolBar86Settings ||
                   _me.ToolBarPSSettings != ToolBarPSSettings ||
                   !ReferenceEquals(_me.SelectedTheme, SelectedTheme);
        }
    }

    public bool ExeDirChanged
    {
        get
        {
            return _me.ExeDir != ExeDir || _me.ExePath != ExePath;
        }
    }

    public string ExeDir
    {
        get => _exe_dir;
        set
        {
            if (_exe_dir != value)
            {
                this.RaiseAndSetIfChanged(ref _exe_dir, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string ExePath
    {
        get => _exe_path;
        set
        {
            if (_exe_path != value)
            {
                this.RaiseAndSetIfChanged(ref _exe_path, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string CFGDir
    {
        get => _cfg_dir;
        set
        {
            if (_cfg_dir != value)
            {
                this.RaiseAndSetIfChanged(ref _cfg_dir, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string ROMDir
    {
        get => _rom_dir;
        set
        {
            if (_rom_dir != value)
            {
                this.RaiseAndSetIfChanged(ref _rom_dir, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }
    public bool IsTrayEnabled
    {
        get => _enable_tray;
        set
        {
            if (_enable_tray != value)
            {
                this.RaiseAndSetIfChanged(ref _enable_tray, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool MinToTray
    {
        get => _min_tray;
        set
        {
            if (_min_tray != value)
            {
                this.RaiseAndSetIfChanged(ref _min_tray, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool MinOnStart
    {
        get => _min_start;
        set
        {
            if (_min_start != value)
            {
                this.RaiseAndSetIfChanged(ref _min_start, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool CloseToTray
    {
        get => _close_tray;
        set
        {
            if (_close_tray != value)
            {
                this.RaiseAndSetIfChanged(ref _close_tray, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool CompactList
    {
        get => _compact_list;
        set
        {
            if (_compact_list != value)
            {
                this.RaiseAndSetIfChanged(ref _compact_list, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool EnableLogging
    {
        get => _enable_logging;
        set
        {
            if (_enable_logging != value)
            {
                this.RaiseAndSetIfChanged(ref _enable_logging, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool AllowInstances
    {
        get => _allow_instances;
        set
        {
            if (_allow_instances != value)
            {
                this.RaiseAndSetIfChanged(ref _allow_instances, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool EnableConsole
    {
        get => _enable_console;
        set
        {
            if (_enable_console != value)
            {
                this.RaiseAndSetIfChanged(ref _enable_console, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool RenameFolders
    {
        get => _rename_folders;
        set
        {
            if (_rename_folders != value)
            {
                this.RaiseAndSetIfChanged(ref _rename_folders, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string LogPath
    {
        get => _log_path;
        set
        {
            if (_log_path != value)
            {
                this.RaiseAndSetIfChanged(ref _log_path, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public ThemeVariant SelectedTheme
    {
        get => _theme;
        set
        {
            if (!ReferenceEquals(value, _theme))
            {
                this.RaiseAndSetIfChanged(ref _theme, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public int SelectedTabIdx { get; set; }

    public ThemeVariant[] Themes { get; private set; }

    public dlgSettingsModel()
    {
        _exe_sub = Executables.Connect()
            .Filter(x => !x.IsDeleted)
            .Bind(out _filtered_executables)
            .Subscribe();

        Themes = [ThemeVariant.Default, ThemeVariant.Light, ThemeVariant.Dark];
    }

    public void Dispose()
    {
        _exe_sub.Dispose();
        Executables.Dispose();
    }

    public void NotifyPropertyChange(AppSettings s)
    {
        if (s.PropertyChanged != null)
        {
            if (_me.CompactList != CompactList)
                s.PropertyChanged(s, new PropertyChangedEventArgs(nameof(AppSettings.CompactMachineList)));
            if (_me.IsTrayEnabled !=  IsTrayEnabled)
                s.PropertyChanged(s, new PropertyChangedEventArgs(nameof(AppSettings.IsTrayEnabled)));
            if (!ReferenceEquals(_me.SelectedTheme, SelectedTheme))
                s.PropertyChanged(s, new PropertyChangedEventArgs(nameof(AppSettings.Theme)));
            if (_me.ToolBarPSSettings != ToolBarPSSettings)
                s.PropertyChanged(s, new PropertyChangedEventArgs(nameof(AppSettings.HasPSToolbarBtn)));
            if (_me.ToolBar86Settings != ToolBar86Settings)
                s.PropertyChanged(s, new PropertyChangedEventArgs(nameof(AppSettings.Has86ToolbarBtn)));
        }
    }

    public void Commit()
    {
        _is_exe_list_changed = false;
        _me = (dlgSettingsModel) MemberwiseClone();

        this.RaisePropertyChanged(nameof(HasChanges));
    }

    public class ExeEntery : ReactiveObject
    {
        private bool _is_default;

        public readonly bool IsNew;
        public bool IsChanged { get; private set; }

        public ExeEntery(bool is_default, bool is_new = true) { IsNew = is_new; _is_default = is_default; }

        public long ID { get; set; }
        public string Name { get; set; }
        public string VMPath { get; set; }
        public string VMRoms { get; set; }
        public string Version { get; set; }
        public string Comment { get; set; }
        public string Arch { get; set; }
        public string Build { get; set; }
        public bool ExeExists { get; set; }
        public bool IsDeleted { get; set; }

        public bool IsDefault 
        { 
            get => _is_default; 
            set
            {
                if (value != _is_default)
                {
                    _is_default = value;
                    IsChanged = true;
                    this.RaisePropertyChanged(nameof(IsChanged));
                }
            }
        }

        public void SetChanged()
        {
            IsChanged = true;
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(VMPath));
            this.RaisePropertyChanged(nameof(VMRoms));
            this.RaisePropertyChanged(nameof(Version));
            this.RaisePropertyChanged(nameof(Comment));
            this.RaisePropertyChanged(nameof(Arch));
            this.RaisePropertyChanged(nameof(Build));
            this.RaisePropertyChanged(nameof(IsChanged));
        }
    }
}