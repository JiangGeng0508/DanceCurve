using Godot;
using System;

namespace DanceCurve.Script;

public partial class SoundEnvelopeController : Node2D
{
    [Signal]
    public delegate void AudioProgressEventHandler(float progress);

    [Signal]
    public delegate void FpsCounterEventHandler(float fps);

    [Signal]
    public delegate void AudioFinishedEventHandler();

    [Signal]
    public delegate void RemoveAudioFromListEventHandler(string name);
    [Signal]
    public delegate void AddAudioToListEventHandler(string name);
    
    [Export] public int WaveformSampleCount { get; set; } = 512;
	[Export] public string CaptureBusName { get; set; } = "SubBus";
	[Export] public int CaptureEffectIndex { get; set; } = 0;
	[Export] public AudioStream PlayingAudio { get; set; } = GD.Load<AudioStream>("res://Audio/maldita.ogg");
	[Export] public float AudioFixDelay { get; set; } = 0.1f;
	[Export] public AudioFinishModeEnum AudioFinishMode = AudioFinishModeEnum.Stop;
	[Export] public float SleepTime = 3f;
 	
	[ExportGroup("BufferA")]
	[Export] public float UpdateInterval { get; set; } = 10f;
	[Export(PropertyHint.Range,"0.0,1.0")] public float EnvelopeSmoothing = 0.4f;
	[Export] public float CompressK = 1.0f;
	
	[ExportGroup("Image")]
	[Export] public Vector2I Resolution { get; set; } = new(640,480);
    
    private const string BufferShaderPath = "res://Shader/sound-envelope-buffer-a.gdshader";
	private const string ImageShaderPath = "res://Shader/sound-envelope-image.gdshader";

	public enum AudioFinishModeEnum
	{
		Stop,
		Loop,
		Next,
	}

	private TextureRect _outputRect;
    private SubViewport[] _viewports = new SubViewport[2];
    private ColorRect[] _passes = new ColorRect[2];
    private ShaderMaterial[] _bufferMaterials = new ShaderMaterial[2];
    private AudioStreamPlayer _effectAudioPlayer;
    private AudioStreamPlayer _realAudioPlayer;
    private ShaderMaterial _imageMaterial;
    private Image _audioImage;
    private ImageTexture _audioTexture;
    private int _frameCount;
	private AudioEffectCapture _capture;
	private float _lastProgressOfReal;
	private float _totalProgress;
	
	public override void _Ready()
	{
        SetupOutput();
        SetupPingPong();
        SetupAudioTexture();
        SetupImagePass();
        SetupAudioPlayer();
        
		// 获取实时采集器（若已配置）
		var busIdx = AudioServer.GetBusIndex(CaptureBusName);
		if (busIdx >= 0 && CaptureEffectIndex >= 0 && CaptureEffectIndex < AudioServer.GetBusEffectCount(busIdx))
		{
			var effect = AudioServer.GetBusEffect(busIdx, CaptureEffectIndex);
			_capture = effect as AudioEffectCapture;
			if (_capture == null)
				GD.PushWarning($"Bus '{CaptureBusName}' effect {CaptureEffectIndex} 不是 AudioEffectCapture。");
		}
		else
		{
			GD.PushWarning($"未找到 AudioEffectCapture：Bus='{CaptureBusName}', Index={CaptureEffectIndex}");
		}
		
		var window = GetTree().GetRoot().GetWindow();
		window.FilesDropped += OnFilesDropped;
	}

    public override void _Process(double delta)
    {
		// 实时抓取音频并推送到音频纹理
		if (_capture != null)
		{
			var framesAvail = _capture.GetFramesAvailable();
			if (framesAvail > 0)
			{
				var vecs = _capture.GetBuffer(framesAvail); //Vector2[512]
				var target = WaveformSampleCount;
				var outBuf = new float[target];
				if (vecs.Length > 0)
				{
					var step = (double)vecs.Length / target;
					for (var i = 0; i < target; i++)
					{
						//采样频率区间分割
						var start = (int)Math.Floor(i * step);
						var end = (int)Math.Floor((i + 1) * step);
						var acc = 0.0;
						for (var j = start; j < end; j++)
						{
							var v = vecs[j];
							acc += 0.5 * (v.X + v.Y); //取均值合并左右声道采样数据
						}
						double denim = Math.Max(1, end - start);//区间长度
						outBuf[i] = (float)(acc / denim);//返回该区间频率密度(均值)
					}
					PushAudioSamples(outBuf);
				}
			}
		}

        var writeIndex = _frameCount % 2;
        var readIndex = 1 - writeIndex;
        

        // 驱动 BufferA 材质
        var prevTexture = _viewports[readIndex].GetTexture();
        var bufMat = _bufferMaterials[writeIndex];
        bufMat.SetShaderParameter("iResolution", (Vector2)Resolution);
        bufMat.SetShaderParameter("iFrame", _frameCount);
        bufMat.SetShaderParameter("iChannel0", prevTexture);
        bufMat.SetShaderParameter("iChannel1", _audioTexture);
        bufMat.SetShaderParameter("uUpdateInterval", UpdateInterval);
        bufMat.SetShaderParameter("uEnvelopeSmoothing",EnvelopeSmoothing);
        bufMat.SetShaderParameter("uCompressK",CompressK);

        // 确保绘制节点使用对应材质
        _passes[writeIndex].Material = bufMat;

        // 更新可见输出材质（Image pass）
        if (_imageMaterial != null)
        {
            _imageMaterial.SetShaderParameter("iResolution", (Vector2)Resolution);
            _imageMaterial.SetShaderParameter("iChannel0", _viewports[writeIndex].GetTexture());
        }

        _frameCount++;
        EmitSignalFpsCounter((float)(1 / delta));
        if(_totalProgress == 0f || !_realAudioPlayer.IsPlaying()) return;
        UpdateProgress();
    }

    private void UpdateProgress()
    {
	    _lastProgressOfReal = _realAudioPlayer.GetPlaybackPosition() + (float)AudioServer.GetTimeSinceLastMix();
	    EmitSignalAudioProgress(_lastProgressOfReal / _totalProgress);
    }

    // 供外部推送音频包络/波形的 API（长度应与 WaveformSampleCount 一致）
    private void PushAudioSamples(float[] samples)
    {
        if (samples == null) return;
        if (samples.Length != WaveformSampleCount)
        {
            GD.PushWarning($"Samples length {samples.Length} != WaveformSampleCount {WaveformSampleCount}. Will clamp or pad.");
        }

        var count = Math.Min(samples.Length, WaveformSampleCount);
        for (var x = 0; x < WaveformSampleCount; x++)
        {
            var v = x < count ? samples[x] : 0.0f;
            _audioImage.SetPixel(x, 0, new Color(v, v, v));
        }
        _audioTexture.Update(_audioImage);
    }

    private void SetupOutput()
    {
        _outputRect = new TextureRect
        {
            Name = "EnvelopeOutput",
            StretchMode = TextureRect.StretchModeEnum.Scale,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        AddChild(_outputRect);
    }

    private void SetupPingPong()
    {
        var bufferShader = GD.Load<Shader>(BufferShaderPath);
        if (bufferShader == null)
            throw new InvalidOperationException($"Failed to load shader: {BufferShaderPath}");

        for (var i = 0; i < 2; i++)
        {
            var vp = new SubViewport
            {
                Name = $"EnvelopeVP_{i}",
                Size = Resolution,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                TransparentBg = false
            };
            AddChild(vp);

            var cr = new ColorRect
            {
                Color = Colors.Black,
                Size = Resolution,
                Material = null
            };
            vp.AddChild(cr);

            var mat = new ShaderMaterial();
            mat.Shader = bufferShader;

            _viewports[i] = vp;
            _passes[i] = cr;
            _bufferMaterials[i] = mat;
        }
    }

    private void SetupImagePass()
    {
        var imageShader = GD.Load<Shader>(ImageShaderPath);
        if (imageShader == null)
            throw new InvalidOperationException($"Failed to load shader: {ImageShaderPath}");

        _imageMaterial = new ShaderMaterial { Shader = imageShader };
        _imageMaterial.SetShaderParameter("iResolution", (Vector2)Resolution);
        _outputRect.Material = _imageMaterial;

        // 将当前写入的 viewport 纹理显示出来（初始为 0）
        //TODO:检查逻辑问题
        _outputRect.Texture = _viewports[0].GetTexture();
    }

    private void SetupAudioTexture()
    {
        _audioImage = Image.CreateEmpty(WaveformSampleCount, 1, false, Image.Format.Rf);
        _audioImage.Fill(new Color(0f, 0f, 0f));
        _audioTexture = ImageTexture.CreateFromImage(_audioImage);
    }

    private void SetupAudioPlayer()
    {
	    if(PlayingAudio == null) return;
	    
	    _effectAudioPlayer ??= InitAudioPlayer(PlayingAudio, "SubBus");
	    _effectAudioPlayer.Play();
	    _realAudioPlayer ??= InitAudioPlayer(PlayingAudio);
	    GetTree().CreateTimer(AudioFixDelay).Timeout += () =>
	    {
		    _realAudioPlayer.Play();
	    };
	    _lastProgressOfReal = 0f;
	    _totalProgress = (float)PlayingAudio.GetLength();
	    if (_realAudioPlayer.HasConnections("finished")) return;
	    GD.Print("Connect to finish");
	    _realAudioPlayer.Finished += OnAudioFinished;
    }

    private void OnAudioFinished()
    {
	    GD.Print($"Audio finished,mode:{AudioFinishMode}");
	    switch (AudioFinishMode)
	    {
		    case AudioFinishModeEnum.Stop:
			    break;
		    case AudioFinishModeEnum.Loop:
			    ReloadPlayer();
			    break;
		    case AudioFinishModeEnum.Next:
			    break;
		    default:
			    GD.PushError("Unsupported AudioFinishMode");
			    return;
	    }
	    EmitSignalAudioFinished();
    }

    private AudioStreamPlayer InitAudioPlayer(AudioStream audioStream, string bus = "Master")
    {
	    var audioStreamPlayer = new AudioStreamPlayer();
	    audioStreamPlayer.Bus = bus;
	    audioStreamPlayer.Stream = audioStream;
	    AddChild(audioStreamPlayer);
	    return audioStreamPlayer;
    }

    private void OnFilesDropped(string[] files)
    {
	    if (files.Length > 1)
	    {
		    GD.PushWarning("Cant dropped more than one file");    
	    }
	    
	    var file = files[0];
	    GD.Print($"file loaded: {file}\n");
	    
	    UpdateAudioStream(file);
    }

    public static AudioStream LoadAudioFromFile(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        switch (file.GetExtension())
	    {
		    case "ogg":
			    return AudioStreamOggVorbis.LoadFromFile(file);
		    case "wav":
			    return AudioStreamWav.LoadFromFile(file);
		    case "mp3":
			    return AudioStreamMP3.LoadFromFile(file);
		    default:
			    GD.PushError("Unsupported file type");
			    return null; 
	    }
    }

    public static AudioStream[] LoadAudioFromFile(string[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var audios = new AudioStream[files.Length];
        
        for (var i = 0; i < files.Length; i++)
        {
	        var audio = LoadAudioFromFile(files[i]);
	        if (audio == null) i--;
	        else audios[i] = audio;
        }
        
        return audios;
    }
    
    private void UpdateAudioStream(string file)
    {
	    var audio = LoadAudioFromFile(file);
	    PlayingAudio = audio;
	    ReloadPlayer();
    }
    private void ReloadPlayer()
    {
	    SetupOutput();
	    SetupPingPong();
	    SetupAudioTexture();
	    SetupImagePass();
	    SetupAudioPlayer();
    }

    private void OnPause(bool paused)
    {
	    if (paused)
	    {
		    UpdateProgress();
		    
		    _effectAudioPlayer.Stop();
		    _realAudioPlayer.Stop();
	    }
	    else if(!_realAudioPlayer.IsPlaying())
	    {
		    _effectAudioPlayer.Play(_lastProgressOfReal - AudioFixDelay);
		    _realAudioPlayer.Play(_lastProgressOfReal);
	    }
    }
}


