using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sonata.Desktop.Models;
using Sonata.Desktop.Services;

namespace Sonata.Desktop.ViewModels;

public class ChatViewModel : INotifyPropertyChanged
{
    private string _currentInput = string.Empty;
    private bool _isBusy;
    private Session? _currentSession;
    private readonly ApiService _apiService;
    private long _sessionLoadVersion;

    public ObservableCollection<Message> Messages { get; } = new();
    public ObservableCollection<Session> Sessions { get; } = new();

    public string CurrentInput
    {
        get => _currentInput;
        set
        {
            if (_currentInput == value) return;
            _currentInput = value;
            OnPropertyChanged();
            SendCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            SendCommand.RaiseCanExecuteChanged();
        }
    }

    public Session? CurrentSession
    {
        get => _currentSession;
        set
        {
            if (_currentSession == value) return;
            SetCurrentSession(value, loadMessages: true);
        }
    }

    public AsyncRelayCommand SendCommand { get; }
    public AsyncRelayCommand NewChatCommand { get; }

    public ChatViewModel()
    {
        _apiService = new ApiService();
        SendCommand = new AsyncRelayCommand(SendAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(CurrentInput));
        NewChatCommand = new AsyncRelayCommand(NewChatAsync);
        _ = LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = await _apiService.GetAllSessionsAsync();

            foreach (var session in sessions)
            {
                Sessions.Add(session);
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Failed to load sessions.");
        }
    }

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentInput) || IsBusy) return;
        IsBusy = true;

        var userText = CurrentInput;
        CurrentInput = string.Empty;
        var session = CurrentSession ?? new Session
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTimeOffset.UtcNow,
        };
        var sessionId = session.Id;

        if (CurrentSession is null)
        {
            Sessions.Add(session);
            SetCurrentSession(session, loadMessages: false);
        }

        Messages.Add(new Message
        {
            SessionId = sessionId,
            Content = userText,
            Role = "user",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        try
        {
            var reply = await _apiService.SendMessageAsync(userText, sessionId);

            Messages.Add(new Message
            {
                SessionId = sessionId,
                Content = reply,
                Role = "assistant",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        catch (Exception)
        {
            Messages.Add(new Message
            {
                SessionId = sessionId,
                Content = "API deu erro",
                Role = "assistant",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task NewChatAsync()
    {
        Messages.Clear();
        CurrentSession = null;
        return Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetCurrentSession(Session? session, bool loadMessages)
    {
        if (_currentSession == session) return;

        _currentSession = session;
        OnPropertyChanged(nameof(CurrentSession));
        SendCommand.RaiseCanExecuteChanged();

        if (loadMessages)
        {
            _ = LoadMessagesAsync(session);
        }
    }

    private async Task LoadMessagesAsync(Session? session)
    {
        var loadVersion = ++_sessionLoadVersion;
        Messages.Clear();

        if (session == null) return;

        try
        {
            var messages = await _apiService.GetMessagesAsync(session.Id);

            if (loadVersion != _sessionLoadVersion || !ReferenceEquals(CurrentSession, session)) return;

            foreach (var message in messages)
            {
                Messages.Add(message);
            }
        }
        catch (Exception)
        {
            if (loadVersion == _sessionLoadVersion)
            {
                Console.WriteLine("Failed to load messages.");
            }
        }
    }
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        try
        {
            if (!CanExecute(parameter)) return;
            await _execute();
        }
        catch (Exception)
        {
            Console.WriteLine("Failed to execute.");
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
