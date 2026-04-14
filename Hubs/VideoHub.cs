using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace Nexus_backend.Hubs
{
    [Authorize]
    public class VideoHub : Hub
    {
        // Store connected users (ConnectionId -> UserId)
        private static readonly ConcurrentDictionary<string, string> _connectedUsers = new();

        // Store user-to-room mapping (ConnectionId -> RoomId)
        private static readonly ConcurrentDictionary<string, string> _userRoom = new();

        // Store room participants (RoomId -> List<ConnectionId>)
        private static readonly ConcurrentDictionary<string, List<string>> _roomParticipants = new();

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                _connectedUsers.TryAdd(Context.ConnectionId, userId);
                await Clients.All.SendAsync("UserOnline", userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                // Remove from connected users
                _connectedUsers.TryRemove(Context.ConnectionId, out _);

                // Remove from room if in one
                if (_userRoom.TryRemove(Context.ConnectionId, out var roomId))
                {
                    if (_roomParticipants.TryGetValue(roomId, out var participants))
                    {
                        participants.Remove(Context.ConnectionId);
                        if (participants.Count == 0)
                        {
                            _roomParticipants.TryRemove(roomId, out _);
                        }
                        else
                        {
                            await Clients.Group(roomId).SendAsync("UserLeft", userId);
                        }
                    }
                }

                await Clients.All.SendAsync("UserOffline", userId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        // ==============================================
        // Room Management Methods
        // ==============================================

        // Join a video call room
        public async Task JoinRoom(string roomId, string targetUserId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            _userRoom.TryAdd(Context.ConnectionId, roomId);

            if (!_roomParticipants.ContainsKey(roomId))
            {
                _roomParticipants[roomId] = new List<string>();
            }

            _roomParticipants[roomId].Add(Context.ConnectionId);

            // Notify others in the room
            await Clients.Group(roomId).SendAsync("UserJoined", userId);

            // Send existing participants to the new user
            var participants = _roomParticipants[roomId]
                .Where(cid => cid != Context.ConnectionId)
                .Select(cid => _connectedUsers.GetValueOrDefault(cid))
                .Where(uid => uid != null)
                .ToList();

            await Clients.Caller.SendAsync("ExistingParticipants", participants);
        }

        // Leave a video call room
        public async Task LeaveRoom(string roomId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

            _userRoom.TryRemove(Context.ConnectionId, out _);

            if (_roomParticipants.TryGetValue(roomId, out var participants))
            {
                participants.Remove(Context.ConnectionId);
                if (participants.Count == 0)
                {
                    _roomParticipants.TryRemove(roomId, out _);
                }
                else
                {
                    await Clients.Group(roomId).SendAsync("UserLeft", userId);
                }
            }
        }

        // ==============================================
        // WebRTC Signaling Methods
        // ==============================================

        // Send WebRTC Offer
        public async Task SendOffer(string targetUserId, string offer, string roomId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            var targetConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == targetUserId).Key;
            if (!string.IsNullOrEmpty(targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", userId, offer, roomId);
            }
        }

        // Send WebRTC Answer
        public async Task SendAnswer(string targetUserId, string answer, string roomId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            var targetConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == targetUserId).Key;
            if (!string.IsNullOrEmpty(targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", userId, answer, roomId);
            }
        }

        // Send ICE Candidate
        public async Task SendIceCandidate(string targetUserId, string candidate, string roomId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            var targetConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == targetUserId).Key;
            if (!string.IsNullOrEmpty(targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", userId, candidate, roomId);
            }
        }

        // ==============================================
        // Incoming Call Management Methods (New)
        // ==============================================

        // Send call offer to target user (for incoming call notification)
        public async Task SendCallOffer(string targetUserId, string callerName, string roomId)
        {
            var callerId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId)) return;

            var targetConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == targetUserId).Key;
            if (!string.IsNullOrEmpty(targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveCallOffer", callerId, callerName, roomId);
            }
        }

        // Accept incoming call
        public async Task AcceptCall(string targetUserId, string roomId)
        {
            var acceptorId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(acceptorId)) return;

            var targetConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == targetUserId).Key;
            if (!string.IsNullOrEmpty(targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("CallAccepted", acceptorId, roomId);
            }
        }

        // Reject incoming call
        public async Task RejectCall(string targetUserId)
        {
            var rejectorId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(rejectorId)) return;

            var targetConnectionId = _connectedUsers.FirstOrDefault(x => x.Value == targetUserId).Key;
            if (!string.IsNullOrEmpty(targetConnectionId))
            {
                await Clients.Client(targetConnectionId).SendAsync("CallRejected", rejectorId);
            }
        }

        // ==============================================
        // Media Control Methods
        // ==============================================

        // Toggle audio
        public async Task ToggleAudio(string roomId, bool isAudioEnabled)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.Group(roomId).SendAsync("UserToggledAudio", userId, isAudioEnabled);
        }

        // Toggle video
        public async Task ToggleVideo(string roomId, bool isVideoEnabled)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.Group(roomId).SendAsync("UserToggledVideo", userId, isVideoEnabled);
        }

        // ==============================================
        // Helper Methods
        // ==============================================

        // Check if user is online
        public Task<bool> IsUserOnline(string userId)
        {
            return Task.FromResult(_connectedUsers.Values.Contains(userId));
        }
    }
}