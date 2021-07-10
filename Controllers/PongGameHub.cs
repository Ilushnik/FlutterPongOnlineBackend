using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FlutterPongOnlineBackend.Controllers
{
    public class PongGameHub : Hub
    {
        private readonly ILogger<PongGameHub> _logger;
        public static ConcurrentDictionary<string, User> UsersInLobby = new ConcurrentDictionary<string, User>();


        public PongGameHub(ILogger<PongGameHub> logger)
        {
            _logger = logger;
        }

        public async Task Login(string user, string position)
        {
            if (UsersInLobby.Count > 2) return;

            _logger.LogInformation("Login: {er} ", user);
            UsersInLobby.AddOrUpdate(Context.ConnectionId, new User(Context.ConnectionId, user, position), (key, oldUser) => new User(key, user, position));

            await Clients.All.SendAsync("GetConnectedUsers",
                UsersInLobby.Select(x => new User(x.Key, x.Value.userName, x.Value.playerPosition)).ToList());

            if (UsersInLobby.Count > 1 && UsersInLobby.Count(u => u.Value.playerPosition != "") == 2)
            {
                var task = Task.Run(async () => {
                    for (var i = 5; i >= 0; i--)
                    {
                        await Task.Delay(1000);
                        _logger.LogInformation("StartGameCountdown " + i);
                        await Clients.All.SendAsync("StartGameCountdown", i);
                    }
                });
                await Task.WhenAll(task);

                _logger.LogInformation("StartGame");
                await Clients.All.SendAsync("StartGame");
            }
        }

        public async Task GetConnectedUsers()
        {
            _logger.LogInformation($"GetConnectedUsers {UsersInLobby.Count}");
            await Clients.All.SendAsync("GetConnectedUsers",
                UsersInLobby.Select(x => new User(x.Key, x.Value.userName, x.Value.playerPosition)).ToList());
        }

        public async Task GetTakenGameSide()
        {
            _logger.LogInformation($"GetTakenGameSide {UsersInLobby.Count}");

            await Clients.Caller.SendAsync("GetTakenGameSide",
                UsersInLobby.Select(x => x.Value.playerPosition).ToList());
        }

        public async Task PlayerMakeAGoal(GameScore gameScore)
        {
            if (gameScore.bottomScore >= 10 || gameScore.topScore >= 10)
            {
                await Clients.All.SendAsync("FinishGame", gameScore);
                foreach (var user in UsersInLobby)
                {
                    user.Value.playerPosition = "";
                }
                await GetConnectedUsers();
                return;
            }
        }

        public async Task UpdateGamePosition(BallPosition ballPosition)
        {
            _logger.LogInformation($"UpdateGamePosition");
            await Clients.Others.SendAsync("UpdateGamePosition", ballPosition);
        }

        public async Task UpdatePlayerPosition(PlayerPosition playerPosition)
        {
            _logger.LogInformation($"UpdatePlayerPosition");
            await Clients.Others.SendAsync("UpdatePlayerPosition", playerPosition);
        }


        public override Task OnDisconnectedAsync(Exception exception)
        {
            UsersInLobby.Remove(Context.ConnectionId, out _);
            Clients.All.SendAsync("GetConnectedUsers", UsersInLobby.Select(x => new User(x.Key, x.Value.userName, x.Value.playerPosition)).ToList());

            Clients.Others.SendAsync("GamerLeft");

            return base.OnDisconnectedAsync(exception);
        }


        public class User
        {
            public string connectionID { get; set; }
            public string userName { get; set; }
            public string playerPosition { get; set; }

            public User(string connectionId, string userName, string position)
            {
                connectionID = connectionId;
                this.userName = userName;
                playerPosition = position;
            }
        }

        public class BallPosition
        {
            public double ballPosX { get; set; }
            public double ballPosY { get; set; }
            public double ballXSpeed { get; set; }
            public double ballYSpeed { get; set; }
            public int ballDirectionX { get; set; }
            public int ballDirectionY { get; set; }
        }

        public class PlayerPosition
        {
            public double playerPosX { get; set; }
        }

        public class GameBoardSize
        {
            public int width { get; set; }
            public int height { get; set; }
        }

        public class GameScore
        {
            public int topScore { get; set; }
            public int bottomScore { get; set; }
        }
    }
}