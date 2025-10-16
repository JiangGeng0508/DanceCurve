using Godot;
using System;

namespace DanceCurve.Script;

public partial class Hud : Control
{
    //TODO: 播放列表
    [Signal] public delegate void ChangeAudioEventHandler(string file);
    [Signal] public delegate void ShowFileWindowEventHandler();
    
    [Export] private float _idleTime = 3.0f;
    [Export] private float _helpTime = 5.0f;

    private Timer _idleTimer;
    private ItemList _audioList;
    
    private string _selectedFile = ""; 
    // private 
    
    public override void _Ready()
    {
        _idleTimer = GetNode<Timer>("IdleTimer");
        _audioList = GetNode<ItemList>("AudioList");
        
		var window = GetTree().GetRoot().GetWindow();
        window.MouseEntered += ActiveAll;
        window.MouseExited += Hide;
        
        Hide();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouse) return;
        
        ActiveAll();
    }

    private void ActiveAll()
    {
        Show();
        _idleTimer.Start(_idleTime);
    }

    private void OnFileSelected(string file)
    {
        _selectedFile = file;
        EmitSignalChangeAudio(file);
    }
    private void OnFileCanceled() => _selectedFile = "";
    private void OnFileConfirmed()
    {
        if(_selectedFile == "") return;
        
        EmitSignalChangeAudio(_selectedFile);
    }

    private void UpdateAudioList()
    {
        var listY = Mathf.Clamp(_audioList.GetItemCount() * 27f + 8f, 0f, 400f);
        _audioList.Size = new Vector2(_audioList.Size.X,listY);
    }

    private void AddAudioList(string name)
    {
        _audioList.AddItem(name);
        UpdateAudioList();
    }

    private void RemoveAudioList(string name)
    {
        for (var i = 0; i < _audioList.ItemCount; i++)
        {
            if (_audioList.GetItemText(i) != name) continue;
            _audioList.RemoveItem(i);
            break;
        }
        UpdateAudioList();
    }
    private void OnIdleTimerTimeout() => Hide();
    private void OnNewAudioButtenPressed() => EmitSignalShowFileWindow();

    private void OnHelpButtonPressed()
    {
        var label = GetNode<Label>("NoticeLabel");
        label.Visible = !label.Visible;
        GetTree().CreateTimer(_helpTime).Timeout += () => label.Visible = false;
    }
}
