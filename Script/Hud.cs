using Godot;
using System;
using Godot.Collections;

public partial class Hud : HBoxContainer
{
    [Signal] public delegate void ChangeAudioEventHandler(string path);
    [Signal] public delegate void ShowFileWindowEventHandler();
    
    [Export] private float _idleTime = 3.0f;

    private Timer _idleTimer;
    
    public static Dictionary<int, string> AudioList = new();

    private string _selectedFile = ""; 
    
    public override void _Ready()
    {
        _idleTimer = GetNode<Timer>("IdleTimer");
        
        AudioList.Add(0, "res://Audio/maldita.ogg");
        AudioList.Add(1, "res://Audio/Ludum Dare 28 03.ogg");
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
    private void OnLocalAudioSelected(int index) => EmitSignalChangeAudio(AudioList[index]);
    private void OnNewAudioButtenPressed() => EmitSignalShowFileWindow();
}
