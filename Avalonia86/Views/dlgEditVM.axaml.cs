using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia86.Core;
using Avalonia86.DialogBox;
using Avalonia86.Tools;
using Avalonia86.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Avalonia86.Views;

public partial class dlgEditVM : BaseWindow
{
    /// <summary>
    /// VM to be edited
    /// </summary>
    private VMVisual _vm;
    private readonly dlgEditModel _m;

    internal VMVisual VM { get { return _vm; } set { _vm = value; } }

    public dlgEditVM() : base("edit_vm")
    {
        InitializeComponent();
        BaseInit();
        _m = new dlgEditModel(Program.Root != null ? AppSettings.Settings : null);
        DataContext = _m;

        Loaded += DlgEditVM_Loaded;

        //Windows 10 workarround
        NativeMSG.SetDarkMode(this);
    }

    private void DlgEditVM_Loaded(object sender, RoutedEventArgs e)
    {
        tbName.Focus();
        tbName.SelectAll();
    }

    private void btnLeftImg_Click(object sender, RoutedEventArgs e)
    {
        var dc = DataContext as dlgEditModel;
        if (dc != null)
            dc.PrevIndex();
    }

    private void btnRightImg_Click(object sender, RoutedEventArgs e)
    {
        var dc = DataContext as dlgEditModel;
        if (dc != null)
            dc.NextIndex();
    }

    // Load the data for selected VM
    private void dlgEditVM_Load(object sender, EventArgs e)
    {
        if (_vm != null)
        {
            _m.Name = _vm.Name;
            _m.Description = _vm.Desc;
            _m.Comment = _vm.Comment;
            _m.Category = _vm.Category;
            _m.Path = _vm.Path;

            var dc = DataContext as dlgEditModel;
            if (dc != null)
            {
                dc.SetIcon(_vm.IconPath);
                dc.SetSelectedExe(_vm.Tag.UID);
                dc.Commit();
            }
        }
        
    }

    private async void btnApply_Click(object sender, RoutedEventArgs e)
    {
        var dc = DataContext as dlgEditModel;

        try
        {
            VMCenter.Edit(_vm.Tag.UID, _m.Name, _m.Path, _m.Description, _m.Category, dc?.VMIcon, _m.Comment, _m.ExeModel.SelectedItem.ID, this);
        }
        catch (Exception ex)
        {
            await this.ShowError($@"Unable to save edit: "+ex.Message, ex);
        }

        //await Dialogs.ShowMessageBox($@"Virtual machine ""{name}"" was successfully modified.",
        //    MessageType.Info, parent, ButtonsType.Ok, "Success");

        Close(DialogResult.Ok);
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(DialogResult.Cancel);
    }
}

internal class dlgEditModel : ReactiveObject
{
    private dlgEditModel _me;
    private string _name, _desc, _com, _path = "< path goes here >";
    private long? _exe_id;
    private readonly bool _rename_folders;
    private string _parent_path = string.Empty;

    private readonly List<string> _img_list;

    private int _index = -1;

    private string _cat;

    public bool HasChanges
    {
        get
        {
            if (_me == null)
                return false;

            return _name != _me._name ||
                   _desc != _me._desc ||
                   _cat != _me._cat ||
                   _com != _me._com ||
                   _exe_id != ExeModel.SelectedItem.ID ||
                   _index != _me._index;
        }
    }

    public string Name
    { 
        get => _name;
        set
        {
            if (_name != value)
            {
                this.RaiseAndSetIfChanged(ref _name, value);
                if (_rename_folders)
                {
                    _path = FolderHelper.EnsureUniqueFolderName(_parent_path, value);
                    this.RaisePropertyChanged(nameof(Path));
                }
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Description
    {
        get => _desc;
        set
        {
            if (_desc != value)
            {
                this.RaiseAndSetIfChanged(ref _desc, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Comment
    {
        get => _com;
        set
        {
            if (_com != value)
            {
                this.RaiseAndSetIfChanged(ref _com, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Path 
    { 
        get => _path; set
        {
            if (value != _path)
            {
                _parent_path = string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    try
                    {
                        var di = new DirectoryInfo(value);
                        _parent_path = di.Parent.FullName;
                    }
                    catch { }
                }

                this.RaiseAndSetIfChanged(ref _path, value);
            }
        }
    }

    public string DefaultCategory { get; private set; }
    public string Default86BoxFolder { get; private set; }
    public string Default86BoxRoms { get; private set; }
    public string Category 
    { 
        get => _cat;
        set
        {
            if (_cat != value)
            {
                this.RaiseAndSetIfChanged(ref _cat, value);
                this.RaisePropertyChanged(nameof(HasChanges));
            }
        }
    }

    public List<string> Categories { get; } = new();

    public ctrlSetExecutableModel ExeModel { get; private set; }

    public string VMIcon
    {
        get
        {
            return _index != -1 ? _img_list[_index] : AppSettings.DefaultIcon;
        }
    }

    public void Commit()
    {
        _exe_id = ExeModel.SelectedItem.ID;
        _me = (dlgEditModel)MemberwiseClone();
    }

    public void NextIndex()
    {
        _index++;
        if (_index == _img_list.Count)
            _index = 0;
        this.RaisePropertyChanged(nameof(VMIcon));
        this.RaisePropertyChanged(nameof(HasChanges));
    }
    public void PrevIndex()
    {
        _index--;
        if (_index < 0)
            _index = _img_list.Count - 1;
        this.RaisePropertyChanged(nameof(VMIcon));
        this.RaisePropertyChanged(nameof(HasChanges));
    }

    public void SetIcon(string path)
    {
        for (int c = 0; c < _img_list.Count; c++)
        {
            if (_img_list[c] == path)
            {
                _index = c;
                this.RaisePropertyChanged(nameof(VMIcon));
                break;
            }
        }
    }

    public void SetSelectedExe(long uid)
    {
        ExeModel.SetSelectedExe(uid, AppSettings.Settings);
    }

    public dlgEditModel(AppSettings s)
    {
        _img_list = AppSettings.GetIconAssets();
        ExeModel = new ctrlSetExecutableModel(s);
        ExeModel.PropertyChanged += ExeModel_PropertyChanged;

        if (s == null)
        {
            //Add a few for the sake of the desiger.
            DefaultCategory = "All machines";
            Categories.Add(DefaultCategory);
            Categories.Add("DOS machines");
            Categories.Add("OS/2 machines");
        }
        else
        {
            foreach (var cat in s.Categories.Items)
                Categories.Add(cat.Name);

            Categories.Sort();

            DefaultCategory = s.DefaultCat.Name;
            _rename_folders = s.RenameFolders;
        }
    }

    private void ExeModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ctrlSetExecutableModel.SelectedItem))
            this.RaisePropertyChanged(nameof(HasChanges));
    }
}