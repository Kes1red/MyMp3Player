using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class SongItem : INotifyPropertyChanged
{
    private int _index;
    private string _title;
    private string _artist;
    private string _duration;
    private bool _isPlaying;
    private bool _isContextMenuOpen;

    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsContextMenuOpen
    {
        get => _isContextMenuOpen;
        set
        {
            _isContextMenuOpen = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Artist
    {
        get => _artist;
        set
        {
            if (_artist != value)
            {
                _artist = value;
                OnPropertyChanged();
            }
        }
    }

    public string Duration
    {
        get => _duration;
        set
        {
            if (_duration != value)
            {
                _duration = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }
    }

    public string FilePath { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? DeleteRequested;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void RequestDelete()
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }
}