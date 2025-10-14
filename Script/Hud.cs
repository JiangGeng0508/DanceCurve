using Godot;
using System;
using Godot.Collections;

public partial class Hud : HBoxContainer
{
    [Signal] public delegate void ChangeAudioEventHandler(string path);
    [Signal] public delegate void ShowFileWindowEventHandler();
    
    [Export] private float _idleTime = 3.0f;

    private Timer _idleTimer;
    
    private string _selectedFile = ""; 
    
    public override void _Ready()
    {
        _idleTimer = GetNode<Timer>("IdleTimer");
        Hide();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouse) return;
        
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
    private void OnIdleTimerTimeout() => Hide();
    private void OnNewAudioButtenPressed() => EmitSignalShowFileWindow();
}
