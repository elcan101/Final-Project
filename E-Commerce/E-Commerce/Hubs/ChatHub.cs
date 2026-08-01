using Microsoft.AspNetCore.SignalR;
using E_Commerce.Data;
using E_Commerce.Models;

namespace E_Commerce.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        public async Task JoinChat(int orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroupName(orderId));
        }

        public async Task SendMessage(int orderId, string senderId, string senderName, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var chatMessage = new ChatMessage
            {
                OrderId = orderId,
                SenderId = senderId,
                SenderName = senderName,
                Message = message.Length > 1000 ? message[..1000] : message,
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            await Clients.Group(ChatGroupName(orderId)).SendAsync("ReceiveMessage", new
            {
                senderId,
                senderName,
                message = chatMessage.Message,
                sentAt = chatMessage.CreatedDate.ToString("HH:mm"),
            });
        }

        private static string ChatGroupName(int orderId) => $"chat-order-{orderId}";
    }
}
