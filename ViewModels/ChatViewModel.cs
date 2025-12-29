using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using LLMOD.Models;

namespace LLMOD.SystemMonitorViewModels;

public partial class ChatViewModel : ObservableObject
{
    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private ObservableCollection<ChatMessage> _messages = new();

    public ChatViewModel()
    {
        // Initial system message
        Messages.Add(new ChatMessage
        {
            Sender = "LLMOD_Core",
            Content = "SYSTEM READY. UPLOAD MODEL OR CONNECT TO ENDPOINT.",
            Timestamp = DateTime.Now
        });
    }

    [RelayCommand]
    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        // Add User Message
        Messages.Add(new ChatMessage { Sender = "User", Content = InputText, Timestamp = DateTime.Now });

        // Simulate Processing / Echo
        var responseContent = $"Processed: {InputText}";
        Messages.Add(new ChatMessage { Sender = "LLMOD_Core", Content = responseContent, Timestamp = DateTime.Now });

        InputText = "";
    }
}