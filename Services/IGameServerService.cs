// Services/IGameServerService.cs
using RobloxGameServerAPI.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RobloxGameServerAPI.Services
{
    public interface IGameServerService
    {
        Task<ServerResponse> GetServerAsync(Guid serverId);
        Task<IEnumerable<ServerResponse>> GetAllServersAsync();
        Task<ServerResponse> CreateServerAsync(CreateServerRequest createRequest);
        Task<ServerResponse> UpdateServerAsync(Guid serverId, UpdateServerRequest updateRequest);
        Task<bool> DeleteServerAsync(Guid serverId);
        Task ProcessServerHeartbeatAsync(Guid serverId);
        Task<IEnumerable<ServerResponse>> GetServersByStatusAsync(string status);
        Task<ServerConfiguration> GetServerConfigurationAsync(Guid serverId);
        Task<ServerConfiguration> UpdateServerConfigurationAsync(Guid serverId, ServerConfigurationUpdateRequest updateRequest);
        Task<ServerHealthInfo> GetServerHealthAsync(Guid serverId);
        Task<PlayerSession> PlayerJoinServerAsync(Guid serverId, PlayerJoinRequest request);
        Task<bool> PlayerLeaveServerAsync(Guid serverId, PlayerLeaveRequest request);
        Task<IEnumerable<PlayerSession>> GetActivePlayerSessionsAsync(Guid serverId);
        Task<IEnumerable<ServerResponse>> GetServersForListingAsync(string statusFilter, string gameModeFilter, string regionFilter, string sortBy, string sortOrder);
    }
}
