namespace Supercell.Laser.Logic.Battle.Objects
{
    using Supercell.Laser.Logic.Battle.Level;
    using Supercell.Laser.Logic.Battle.Structures;
    using Supercell.Laser.Logic.Data.Helper;
    using Supercell.Laser.Logic.Helper;
    using Supercell.Laser.Titan.DataStream;

    public class GameObjectManager
    {
        private Queue<GameObject> AddObjects;
        private Queue<GameObject> RemoveObjects;

        private BattleMode Battle;
        private List<GameObject> GameObjects;

        private int ObjectCounter;

        public GameObjectManager(BattleMode battle)
        {
            Battle = battle;
            GameObjects = new List<GameObject>();

            AddObjects = new Queue<GameObject>();
            RemoveObjects = new Queue<GameObject>();
        }

        public GameObject[] GetGameObjects()
        {
            return GameObjects.ToArray();
        }

        public BattleMode GetBattle()
        {
            return Battle;
        }

        public void PreTick()
        {
            foreach (GameObject gameObject in GameObjects)
            {
                if (gameObject.ShouldDestruct())
                {
                    gameObject.OnDestruct();
                    RemoveGameObject(gameObject);
                }
                else
                {
                    gameObject.PreTick();
                }
            }

            while (AddObjects.Count > 0)
            {
                GameObjects.Add(AddObjects.Dequeue());
            }

            while (RemoveObjects.Count > 0)
            {
                GameObjects.Remove(RemoveObjects.Dequeue());
            }
        }

        public void Tick()
        {
            foreach (GameObject gameObject in GameObjects)
            {
                gameObject.Tick();
            }
        }

        public void AddGameObject(GameObject gameObject)
        {
            gameObject.AttachGameObjectManager(this, GlobalId.CreateGlobalId(gameObject.GetObjectType(), ObjectCounter++));
            AddObjects.Enqueue(gameObject);
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            RemoveObjects.Enqueue(gameObject);
        }

        public GameObject GetGameObjectByID(int globalId)
        {
            return GameObjects.Find(obj => obj.GetGlobalID() == globalId);
        }

        public List<GameObject> GetVisibleGameObjects(int teamIndex)
        {
            List<GameObject> objects = new List<GameObject>();

            foreach (GameObject obj in GameObjects)
            {
                if (obj.GetFadeCounter() > 0 || obj.GetIndex() / 16 == teamIndex)
                {
                    objects.Add(obj);
                }
            }

            return objects;
        }

        public void Encode(BitStream bitStream, TileMap tileMap, int ownObjectGlobalId, int playerIndex, int teamIndex)
        {
            BattlePlayer[] players = Battle.GetPlayers();
            List<GameObject> visibleGameObjects = GetVisibleGameObjects(teamIndex);

            int GameModeVariation = Battle.GetGameModeVariation();
            bitStream.WritePositiveInt(ownObjectGlobalId, 21);

            if (GameModeVariation == 0)
            {
                bitStream.WritePositiveVInt(Battle.GetGemGrabCountdown(), 4);
            }

            bitStream.WriteBoolean(false);
            bitStream.WriteInt(-1, 4);

            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);

            if (tileMap.Width < 22)
            {
                bitStream.WritePositiveInt(0, 5); // 0xa820a8
                bitStream.WritePositiveInt(0, 6); // 0xa820b4
                bitStream.WritePositiveInt(tileMap.Width - 1, 5); // 0xa820c0
            }
            else
            {
                bitStream.WritePositiveInt(0, 6); // 0xa820a8
                bitStream.WritePositiveInt(0, 6); // 0xa820b4
                bitStream.WritePositiveInt(tileMap.Width - 1, 6); // 0xa820c0
            }
            bitStream.WritePositiveInt(tileMap.Height - 1, 6); // 0xa820d0

            for (int i = 0; i < tileMap.Width; i++)
            {
                for (int j = 0; j < tileMap.Height; j++)
                {
                    var tile = tileMap.GetTile(i, j, true);
                    if (tile.Data.RespawnSeconds > 0 || tile.Data.IsDestructible)
                    {
                        bitStream.WriteBoolean(tile.IsDestructed());
                    }
                }
            }


            bitStream.WritePositiveInt(1, 1);


            for (int i = 0; i < players.Length; i++)
            {
                bitStream.WritePositiveInt(0, 1);
                bitStream.WriteBoolean(players[i].HasUlti());
                if (GameModeVariation == 6)
                {
                    bitStream.WritePositiveInt(0, 4);
                }
                if (i == playerIndex)
                {
                    bitStream.WritePositiveInt(players[i].GetUltiCharge(), 12);
                    bitStream.WritePositiveInt(0, 1);
                    bitStream.WritePositiveInt(0, 1);
                }
            }

            bitStream.WritePositiveInt(1, 1);

            switch (GameModeVariation)
            {
                case 6:
                    bitStream.WritePositiveInt(Battle.GetPlayersAliveCountForBattleRoyale(), 4);
                    break;
            }

            for (int i = 0; i < players.Length; i++)
            {
                if (GameModeVariation != 6)
                {
                    bitStream.WriteBoolean(true);
                    bitStream.WritePositiveVIntMax255(players[i].GetScore());
                }
                else
                {
                    bitStream.WriteBoolean(false);
                }
                if (bitStream.WriteBoolean(players[i].KillList.Count > 0))
                {
                    bitStream.WritePositiveIntMax15(players[i].KillList.Count);
                    for (int j = 0; j < players[i].KillList.Count; j++)
                    {
                        bitStream.WritePositiveIntMax15(players[i].KillList[j].PlayerIndex);
                        bitStream.WriteIntMax7(players[i].KillList[j].BountyStarsEarned);
                    }
                }
            }

           // bitStream.WritePositiveIntMax7(0);

            bitStream.WritePositiveInt(visibleGameObjects.Count, 8);

            foreach (GameObject gameObject in visibleGameObjects)
            {
                ByteStreamHelper.WriteDataReference(bitStream, gameObject.GetDataId());
            }

            foreach (GameObject gameObject in visibleGameObjects)
            {
                bitStream.WritePositiveInt(GlobalId.GetInstanceId(gameObject.GetGlobalID()), 14); // 0x2381b4
            }

            foreach (GameObject gameObject in visibleGameObjects)
            {
                gameObject.Encode(bitStream, gameObject.GetGlobalID() == ownObjectGlobalId, teamIndex);
            }

            bitStream.WritePositiveInt(0, 8);
            /*bitStream.WritePositiveInt(16, 5);
            bitStream.WritePositiveInt(8, 7);
            bitStream.WritePositiveInt(16, 5);
            bitStream.WritePositiveInt(2, 7);
            bitStream.WritePositiveInt(16, 5);
            bitStream.WritePositiveInt(24, 7);
            bitStream.WritePositiveInt(16, 5);
            bitStream.WritePositiveInt(10, 7);
            bitStream.WritePositiveInt(16, 5);
            bitStream.WritePositiveInt(22, 7);
            bitStream.WritePositiveInt(18, 5);
            bitStream.WritePositiveInt(5, 7);
            bitStream.WritePositiveInt(18, 5);
            bitStream.WritePositiveInt(3, 7);*/

            //   bitStream.WritePositiveInt(0, 14);
            /*bitStream.WritePositiveInt(1, 14);
            bitStream.WritePositiveInt(2, 14);
            bitStream.WritePositiveInt(3, 14);
            bitStream.WritePositiveInt(4, 14);
            bitStream.WritePositiveInt(5, 14);
            bitStream.WritePositiveInt(0, 14);
            bitStream.WritePositiveInt(1, 14);*/

            /*
            bitStream.WritePositiveVInt(2550, 4);
            bitStream.WritePositiveVInt(150, 4);
            bitStream.WritePositiveVInt(0, 3); // 16 это когда негры бегают
            bitStream.WritePositiveVInt(0, 4);
            bitStream.WritePositiveInt(10, 4);

            bitStream.WriteBoolean(false);

            bitStream.WritePositiveInt(0, 3); // State
            bitStream.WriteBoolean(false);
            bitStream.WriteInt(63, 6); // Animation ticks

            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);

            bitStream.WriteBoolean(true); // 1
            bitStream.WriteBoolean(true); // 1
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WritePositiveInt(1337, 13);
            bitStream.WritePositiveInt(1337, 13);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 4);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 9);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 5);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveInt(3000, 12);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);

            bitStream.WritePositiveVInt(2550, 4);
            bitStream.WritePositiveVInt(9750, 4);
            bitStream.WritePositiveVInt(1, 3);
         //   bitStream.WriteBoolean(true);
            bitStream.WritePositiveVInt(0, 4);
          //  bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(10, 4);
            bitStream.WritePositiveInt(270, 9);
            bitStream.WritePositiveInt(270, 9);
            bitStream.WritePositiveInt(4, 3);
            bitStream.WriteInt(63, 6);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WritePositiveInt(5320, 13);
            bitStream.WritePositiveInt(5320, 13);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 9);
            bitStream.WritePositiveInt(0, 5);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveInt(3000, 12);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveVInt(3150, 4);
            bitStream.WritePositiveVInt(9750, 4);
            bitStream.WritePositiveVInt(2, 3);
            bitStream.WritePositiveVInt(0, 4);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(10, 4);
            bitStream.WritePositiveInt(270, 9);
            bitStream.WritePositiveInt(270, 9);
            bitStream.WritePositiveInt(4, 3);
            bitStream.WriteInt(63, 6);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WritePositiveInt(7280, 13);
            bitStream.WritePositiveInt(7280, 13);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 9);
            bitStream.WritePositiveInt(0, 5);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveInt(3000, 12);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveVInt(3750, 4);
            bitStream.WritePositiveVInt(9750, 4);
            bitStream.WritePositiveVInt(3, 3);
            bitStream.WritePositiveVInt(0, 4);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(10, 4);
            bitStream.WritePositiveInt(270, 9);
            bitStream.WritePositiveInt(270, 9);
            bitStream.WritePositiveInt(4, 3);
            bitStream.WriteInt(63, 6);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WritePositiveInt(7000, 13);
            bitStream.WritePositiveInt(7000, 13);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 9);
            bitStream.WritePositiveInt(0, 5);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveInt(3000, 12);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveVInt(3150, 4);
            bitStream.WritePositiveVInt(150, 4);
            bitStream.WritePositiveVInt(20, 3);
            bitStream.WritePositiveVInt(0, 4);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(10, 4);
            bitStream.WritePositiveInt(90, 9);
            bitStream.WritePositiveInt(90, 9);
            bitStream.WritePositiveInt(4, 3);
            bitStream.WriteInt(63, 6);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WritePositiveVInt(8120, 4);
            bitStream.WritePositiveVInt(8120, 4);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 9);
            bitStream.WritePositiveInt(0, 5);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveInt(3000, 12);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveVInt(3750, 4);
            bitStream.WritePositiveVInt(150, 4);
            bitStream.WritePositiveVInt(21, 3);
            bitStream.WritePositiveVInt(0, 4);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(10, 4);
            bitStream.WritePositiveInt(90, 9);
            bitStream.WritePositiveInt(90, 9);
            bitStream.WritePositiveInt(4, 3);
            bitStream.WriteInt(63, 6);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WritePositiveInt(3080, 13);
            bitStream.WritePositiveInt(3080, 13);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 2);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(0, 9);
            bitStream.WritePositiveInt(0, 5);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveInt(3000, 12);
            bitStream.WriteBoolean(true);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(true);
            bitStream.WritePositiveVInt(3150, 4);
            bitStream.WritePositiveVInt(4950, 4);
            bitStream.WritePositiveVInt(102, 3);
            bitStream.WritePositiveVInt(0, 4);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(10, 4);
            bitStream.WritePositiveInt(260, 14);
            bitStream.WritePositiveInt(40, 14);
            bitStream.WritePositiveVInt(3157, 4);
            bitStream.WritePositiveVInt(5419, 4);
            bitStream.WritePositiveVInt(102, 3);
            bitStream.WritePositiveVInt(0, 4);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveInt(10, 4);*/

            /* bitStream.WritePositiveInt(ownObjectGlobalId, 21); // 0xa81858
             bitStream.WriteBoolean(false); // 0xa81868

             for (int i = 0; i < players.Length; i++)
             {
                 bitStream.WritePositiveInt(0, 2); // 0xa81a7c
                 bitStream.WriteBoolean(false); // 0xa81a44
             }

             if (Battle.GetGameModeVariation() == 0) bitStream.WritePositiveVInt(0, 4); // 0xa81d3c

             bitStream.WriteBoolean(false); // 0xa81fec
             bitStream.WriteInt(-1, 4); // 0xa81ff8

             bitStream.WriteBoolean(true); // 0xa82040
             bitStream.WriteBoolean(true); // 0xa8204c
             bitStream.WriteBoolean(true); // 0xa82058
             bitStream.WriteBoolean(false); // 0xa82064

             if (tileMap.Width < 22)
             {
                 bitStream.WritePositiveInt(0, 5); // 0xa820a8
                 bitStream.WritePositiveInt(0, 6); // 0xa820b4
                 bitStream.WritePositiveInt(tileMap.Width - 1, 5); // 0xa820c0
             }
             else
             {
                 bitStream.WritePositiveInt(0, 6); // 0xa820a8
                 bitStream.WritePositiveInt(0, 6); // 0xa820b4
                 bitStream.WritePositiveInt(tileMap.Width - 1, 6); // 0xa820c0
             }
             bitStream.WritePositiveInt(tileMap.Height - 1, 6); // 0xa820d0

             for (int i = 0; i < tileMap.Width; i++)
             {
                 for (int j = 0; j < tileMap.Height; j++)
                 {
                     var tile = tileMap.GetTile(i, j, true);
                     if (tile.Data.RestoreAfterDynamicOverlap || tile.Data.IsDestructible)
                     {
                         bitStream.WriteBoolean(tile.IsDestructed());
                     }
                 }
             }

             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa825f4
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa829d8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa82b2c

             for (int i = 0; i < players.Length; i++)
             {
                 bitStream.WriteBoolean(true); // 0xa83344
                 bitStream.WriteBoolean(players[i].HasUlti()); // 0xa83350
                 bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa83408
                 bitStream.WriteBoolean(false); // 0xa83414
                 bitStream.WriteBoolean(false); // 0xa83420
                 bitStream.WriteBoolean(players[i].IsUsingPin(Battle.GetTicksGone())); // 0xa8342c
                 if (players[i].IsUsingPin(Battle.GetTicksGone()))
                 {
                     bitStream.WriteInt(players[i].GetPinIndex(), 3);
                     bitStream.WritePositiveInt(players[i].GetPinUseCooldown(Battle.GetTicksGone()), 14);
                 }

                 if (i == playerIndex)
                 {
                     bitStream.WritePositiveInt(players[i].GetUltiCharge(), 12); // 0xa834b4
                     bitStream.WriteBoolean(false); // 0xa834c0
                     bitStream.WriteBoolean(false); // 0xa834cc
                     bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa834dc

                     switch (Battle.GetGameModeVariation())
                     {
                         case 0:
                             bitStream.WriteBoolean(true); // 0xa834f8
                             bitStream.WritePositiveInt(0, 2); // 0xa83510
                             bitStream.WritePositiveInt(0, 14); // 0xa8351c
                             break;
                         case 25:
                             bitStream.WriteBoolean(false); // 0xa834f8
                             break;
                     }

                 }
             }

             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa8355c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa8356c
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa83594

             for (int i = 0; i < players.Length; i++)
             {
                 if (Battle.GetGameModeVariation() == 0 || Battle.GetGameModeVariation() == 25)
                 {
                     bitStream.WriteBoolean(true); // 0xa8374c
                     bitStream.WritePositiveVIntMax255(players[i].GetScore());
                 }
                 else
                 {
                     bitStream.WriteBoolean(false); // 0xa8374c
                 }
                 bitStream.WriteBoolean(false); // 0xa83798
             }

             bitStream.WritePositiveVInt(visibleGameObjects.Count, 4);

             foreach (LogicGameObject gameObject in visibleGameObjects)
             {
                 ByteStreamHelper.WriteDataReference(bitStream, gameObject.GetDataId());
             }

             foreach (LogicGameObject gameObject in visibleGameObjects)
             {
                 bitStream.WritePositiveVInt(GlobalId.GetInstanceId(gameObject.GetGlobalID()), 4); // 0x2381b4
             }

             bitStream.WriteBoolean(false);

             foreach (LogicGameObject gameObject in visibleGameObjects)
             {
                 gameObject.Encode(bitStream, gameObject.GetGlobalID() == ownObjectGlobalId, teamIndex);
             } */

            /* bitStream.WritePositiveVInt(7, 4); // 0xa8381c
             bitStream.WritePositiveInt(16, 5); // 0x244f08
             bitStream.WritePositiveInt(0, 10); // 0x244f1c
             bitStream.WritePositiveInt(16, 5); // 0x244f08
             bitStream.WritePositiveInt(25, 10); // 0x244f1c
             bitStream.WritePositiveInt(16, 5); // 0x244f08
             bitStream.WritePositiveInt(50, 10); // 0x244f1c
             bitStream.WritePositiveInt(16, 5); // 0x244f08
             bitStream.WritePositiveInt(36, 10); // 0x244f1c
             bitStream.WritePositiveInt(16, 5); // 0x244f08
             bitStream.WritePositiveInt(22, 10); // 0x244f1c
             bitStream.WritePositiveInt(16, 5); // 0x244f08
             bitStream.WritePositiveInt(43, 10); // 0x244f1c
             bitStream.WritePositiveInt(18, 5); // 0x244f08
             bitStream.WritePositiveInt(5, 10); // 0x244f1c
             bitStream.WritePositiveVInt(0, 4); // 0x2381b4
             bitStream.WritePositiveVInt(1, 4); // 0x2381b4
             bitStream.WritePositiveVInt(2, 4); // 0x2381b4
             bitStream.WritePositiveVInt(3, 4); // 0x2381b4
             bitStream.WritePositiveVInt(4, 4); // 0x2381b4
             bitStream.WritePositiveVInt(5, 4); // 0x2381b4
             bitStream.WritePositiveVInt(0, 4); // 0x2381b4
             bitStream.WriteBoolean(false); // 0xa847b0
             bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
             bitStream.WritePositiveVInt(9750, 4); // 0x8bb9ec
             bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
             bitStream.WritePositiveVInt(0, 3); // я тебе жажку в рот запихал
             bitStream.WritePositiveInt(10, 4); // 0x8bba50
             bitStream.WriteBoolean(false); // 0xa1afec
             bitStream.WriteBoolean(false); // 0xa1aff8
             bitStream.WritePositiveInt(4, 3); // 0xa1b034
             bitStream.WriteBoolean(false); // 0xa1b054
             bitStream.WriteInt(63, 6); // 0xa1b060
             bitStream.WriteBoolean(false); // 0xa1b06c
             bitStream.WriteBoolean(false); // 0xa1b078
             bitStream.WriteBoolean(false); // 0xa1b09c
             bitStream.WriteBoolean(false); // 0xa1b0a8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
             bitStream.WriteBoolean(false); // 0xa1b0f0
             bitStream.WriteBoolean(false); // 0xa1b0fc
             bitStream.WriteBoolean(false); // 0xa1b108
             bitStream.WriteBoolean(false); // 0xa1b114
             bitStream.WriteBoolean(false); // 0xa1b120
             bitStream.WriteBoolean(false); // 0xa1b12c
             bitStream.WriteBoolean(false); // 0xa1b154
             bitStream.WriteBoolean(false); // 0xa1b160
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
             bitStream.WriteBoolean(false); // 0xa1b308
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
             bitStream.WritePositiveVInt(3800, 4); // 0xa1b39c
             bitStream.WritePositiveVInt(3800, 4); // 0xa1b3a8
             bitStream.WriteBoolean(false); // 0xa1b3e8
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
             bitStream.WriteBoolean(false); // 0xa1b478
             bitStream.WriteBoolean(false); // 0xa1b4ac
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
             bitStream.WriteBoolean(false); // 0xa1b550
             bitStream.WriteBoolean(false); // 0xa1b5c0
             bitStream.WriteBoolean(false); // 0xa1b5cc
             bitStream.WriteBoolean(false); // 0xa1b660
             bitStream.WriteBoolean(false); // 0xa1b75c
             bitStream.WriteBoolean(false); // 0xa1b9a0
             bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
             bitStream.WriteBoolean(false); // 0xa1b9b8
             bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
             bitStream.WriteBoolean(false); // 0xa1b9d8
             bitStream.WriteBoolean(false); // 0xa1b9f8
             bitStream.WritePositiveInt(0, 5); // 0xa1ba10
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveInt(3000, 12); // 0x15a09c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
             bitStream.WritePositiveVInt(9750, 4); // 0x8bb9ec
             bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
             bitStream.WritePositiveVInt(1, 3); // 0x8bba04
             bitStream.WritePositiveInt(10, 4); // 0x8bba50
             bitStream.WritePositiveInt(270, 9); // 0xa1b018
             bitStream.WritePositiveInt(270, 9); // 0xa1b024
             bitStream.WritePositiveInt(4, 3); // 0xa1b034
             bitStream.WriteBoolean(false); // 0xa1b054
             bitStream.WriteInt(63, 6); // 0xa1b060
             bitStream.WriteBoolean(false); // 0xa1b06c
             bitStream.WriteBoolean(false); // 0xa1b078
             bitStream.WriteBoolean(false); // 0xa1b09c
             bitStream.WriteBoolean(false); // 0xa1b0a8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
             bitStream.WriteBoolean(false); // 0xa1b0f0
             bitStream.WriteBoolean(false); // 0xa1b0fc
             bitStream.WriteBoolean(false); // 0xa1b108
             bitStream.WriteBoolean(false); // 0xa1b114
             bitStream.WriteBoolean(false); // 0xa1b120
             bitStream.WriteBoolean(false); // 0xa1b12c
             bitStream.WriteBoolean(false); // 0xa1b154
             bitStream.WriteBoolean(false); // 0xa1b160
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
             bitStream.WriteBoolean(false); // 0xa1b308
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
             bitStream.WritePositiveVInt(4400, 4); // 0xa1b39c
             bitStream.WritePositiveVInt(4400, 4); // 0xa1b3a8
             bitStream.WriteBoolean(false); // 0xa1b3e8
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
             bitStream.WriteBoolean(false); // 0xa1b478
             bitStream.WriteBoolean(false); // 0xa1b4ac
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
             bitStream.WriteBoolean(false); // 0xa1b550
             bitStream.WriteBoolean(false); // 0xa1b5c0
             bitStream.WriteBoolean(false); // 0xa1b5cc
             bitStream.WriteBoolean(false); // 0xa1b75c
             bitStream.WriteBoolean(false); // 0xa1b9a0
             bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
             bitStream.WriteBoolean(false); // 0xa1b9b8
             bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
             bitStream.WritePositiveInt(0, 5); // 0xa1ba10
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveInt(1000, 12); // 0x15a09c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveVInt(2250, 4); // 0x8bb9e0 150 3150
             bitStream.WritePositiveVInt(150, 4); // 0x8bb9ec 150 9750
             bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
             bitStream.WritePositiveVInt(16, 3); // 0x8bba04
             bitStream.WritePositiveInt(10, 4); // 0x8bba50
             bitStream.WritePositiveInt(270, 9); // 0xa1b018
             bitStream.WritePositiveInt(270, 9); // 0xa1b024
             bitStream.WritePositiveInt(4, 3); // 0xa1b034
             bitStream.WriteBoolean(false); // 0xa1b054
             bitStream.WriteInt(63, 6); // 0xa1b060
             bitStream.WriteBoolean(false); // 0xa1b06c
             bitStream.WriteBoolean(false); // 0xa1b078
             bitStream.WriteBoolean(false); // 0xa1b09c
             bitStream.WriteBoolean(false); // 0xa1b0a8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
             bitStream.WriteBoolean(false); // 0xa1b0f0
             bitStream.WriteBoolean(false); // 0xa1b0fc
             bitStream.WriteBoolean(false); // 0xa1b108
             bitStream.WriteBoolean(false); // 0xa1b114
             bitStream.WriteBoolean(false); // 0xa1b120
             bitStream.WriteBoolean(false); // 0xa1b12c
             bitStream.WriteBoolean(false); // 0xa1b154
             bitStream.WriteBoolean(false); // 0xa1b160
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
             bitStream.WriteBoolean(false); // 0xa1b308
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
             bitStream.WritePositiveVInt(3400, 4); // 0xa1b39c
             bitStream.WritePositiveVInt(3400, 4); // 0xa1b3a8
             bitStream.WriteBoolean(false); // 0xa1b3e8
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
             bitStream.WriteBoolean(false); // 0xa1b478
             bitStream.WriteBoolean(false); // 0xa1b4ac
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
             bitStream.WriteBoolean(false); // 0xa1b550
             bitStream.WriteBoolean(false); // 0xa1b5c0
             bitStream.WriteBoolean(false); // 0xa1b5cc
             bitStream.WriteBoolean(false); // 0xa1b75c
             bitStream.WriteBoolean(false); // 0xa1b9a0
             bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
             bitStream.WriteBoolean(false); // 0xa1b9b8
             bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
             bitStream.WritePositiveInt(0, 5); // 0xa1ba10
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveInt(3000, 12); // 0x15a09c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
             bitStream.WritePositiveVInt(9750, 4); // 0x8bb9ec
             bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
             bitStream.WritePositiveVInt(3, 3); // 0x8bba04
             bitStream.WritePositiveInt(10, 4); // 0x8bba50
             bitStream.WritePositiveInt(270, 9); // 0xa1b018
             bitStream.WritePositiveInt(270, 9); // 0xa1b024
             bitStream.WritePositiveInt(4, 3); // 0xa1b034
             bitStream.WriteBoolean(false); // 0xa1b054
             bitStream.WriteInt(63, 6); // 0xa1b060
             bitStream.WriteBoolean(false); // 0xa1b06c
             bitStream.WriteBoolean(false); // 0xa1b078
             bitStream.WriteBoolean(false); // 0xa1b09c
             bitStream.WriteBoolean(false); // 0xa1b0a8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
             bitStream.WriteBoolean(false); // 0xa1b0f0
             bitStream.WriteBoolean(false); // 0xa1b0fc
             bitStream.WriteBoolean(false); // 0xa1b108
             bitStream.WriteBoolean(false); // 0xa1b114
             bitStream.WriteBoolean(false); // 0xa1b120
             bitStream.WriteBoolean(false); // 0xa1b12c
             bitStream.WriteBoolean(false); // 0xa1b154
             bitStream.WriteBoolean(false); // 0xa1b160
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
             bitStream.WriteBoolean(false); // 0xa1b308
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
             bitStream.WritePositiveVInt(2600, 4); // 0xa1b39c
             bitStream.WritePositiveVInt(2600, 4); // 0xa1b3a8
             bitStream.WriteBoolean(false); // 0xa1b3e8
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
             bitStream.WriteBoolean(false); // 0xa1b478
             bitStream.WriteBoolean(false); // 0xa1b4ac
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
             bitStream.WriteBoolean(false); // 0xa1b550
             bitStream.WriteBoolean(false); // 0xa1b5c0
             bitStream.WriteBoolean(false); // 0xa1b5cc
             bitStream.WriteBoolean(false); // 0xa1b75c
             bitStream.WriteBoolean(false); // 0xa1b9a0
             bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
             bitStream.WriteBoolean(false); // 0xa1b9b8
             bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
             bitStream.WritePositiveInt(0, 5); // 0xa1ba10
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveInt(3000, 12); // 0x15a09c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
             bitStream.WritePositiveVInt(150, 4); // 0x8bb9ec
             bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
             bitStream.WritePositiveVInt(20, 3); // 0x8bba04
             bitStream.WritePositiveInt(10, 4); // 0x8bba50
             bitStream.WritePositiveInt(90, 9); // 0xa1b018
             bitStream.WritePositiveInt(90, 9); // 0xa1b024
             bitStream.WritePositiveInt(4, 3); // 0xa1b034
             bitStream.WriteBoolean(false); // 0xa1b054
             bitStream.WriteInt(63, 6); // 0xa1b060
             bitStream.WriteBoolean(false); // 0xa1b06c
             bitStream.WriteBoolean(false); // 0xa1b078
             bitStream.WriteBoolean(false); // 0xa1b09c
             bitStream.WriteBoolean(false); // 0xa1b0a8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
             bitStream.WriteBoolean(false); // 0xa1b0f0
             bitStream.WriteBoolean(false); // 0xa1b0fc
             bitStream.WriteBoolean(false); // 0xa1b108
             bitStream.WriteBoolean(false); // 0xa1b114
             bitStream.WriteBoolean(false); // 0xa1b120
             bitStream.WriteBoolean(false); // 0xa1b12c
             bitStream.WriteBoolean(false); // 0xa1b154
             bitStream.WriteBoolean(false); // 0xa1b160
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
             bitStream.WriteBoolean(false); // 0xa1b308
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
             bitStream.WritePositiveVInt(2200, 4); // 0xa1b39c
             bitStream.WritePositiveVInt(2200, 4); // 0xa1b3a8
             bitStream.WriteBoolean(false); // 0xa1b3e8
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
             bitStream.WriteBoolean(false); // 0xa1b478
             bitStream.WriteBoolean(false); // 0xa1b4ac
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
             bitStream.WriteBoolean(false); // 0xa1b550
             bitStream.WriteBoolean(false); // 0xa1b5c0
             bitStream.WriteBoolean(false); // 0xa1b5cc
             bitStream.WriteBoolean(false); // 0xa1b71c
             bitStream.WriteBoolean(false); // 0xa1b75c
             bitStream.WriteBoolean(false); // 0xa1b9a0
             bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
             bitStream.WriteBoolean(false); // 0xa1b9b8
             bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
             bitStream.WritePositiveInt(0, 5); // 0xa1ba10
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveInt(3000, 12); // 0x15a09c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
             bitStream.WritePositiveVInt(150, 4); // 0x8bb9ec
             bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
             bitStream.WritePositiveVInt(21, 3); // 0x8bba04
             bitStream.WritePositiveInt(10, 4); // 0x8bba50
             bitStream.WritePositiveInt(90, 9); // 0xa1b018
             bitStream.WritePositiveInt(90, 9); // 0xa1b024
             bitStream.WritePositiveInt(4, 3); // 0xa1b034
             bitStream.WriteBoolean(false); // 0xa1b054
             bitStream.WriteInt(63, 6); // 0xa1b060
             bitStream.WriteBoolean(false); // 0xa1b06c
             bitStream.WriteBoolean(false); // 0xa1b078
             bitStream.WriteBoolean(false); // 0xa1b09c
             bitStream.WriteBoolean(false); // 0xa1b0a8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
             bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
             bitStream.WriteBoolean(false); // 0xa1b0f0
             bitStream.WriteBoolean(false); // 0xa1b0fc
             bitStream.WriteBoolean(false); // 0xa1b108
             bitStream.WriteBoolean(false); // 0xa1b114
             bitStream.WriteBoolean(false); // 0xa1b120
             bitStream.WriteBoolean(false); // 0xa1b12c
             bitStream.WriteBoolean(false); // 0xa1b154
             bitStream.WriteBoolean(false); // 0xa1b160
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
             bitStream.WriteBoolean(false); // 0xa1b308
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
             bitStream.WritePositiveVInt(2800, 4); // 0xa1b39c
             bitStream.WritePositiveVInt(2800, 4); // 0xa1b3a8
             bitStream.WriteBoolean(false); // 0xa1b3e8
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
             bitStream.WriteBoolean(false); // 0xa1b478
             bitStream.WriteBoolean(false); // 0xa1b4ac
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
             bitStream.WriteBoolean(false); // 0xa1b550
             bitStream.WriteBoolean(false); // 0xa1b5c0
             bitStream.WriteBoolean(false); // 0xa1b5cc
             bitStream.WriteBoolean(false); // 0xa1b75c
             bitStream.WriteBoolean(false); // 0xa1b9a0
             bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
             bitStream.WriteBoolean(false); // 0xa1b9b8
             bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
             bitStream.WritePositiveInt(0, 5); // 0xa1ba10
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveInt(3000, 12); // 0x15a09c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
             bitStream.WriteBoolean(false); // 0x15a05c
             bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
             bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
             bitStream.WritePositiveVInt(4950, 4); // 0x8bb9ec
             bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
             bitStream.WritePositiveVInt(102, 3); // 0x8bba04
             bitStream.WritePositiveInt(10, 4); // 0x8bba50
             bitStream.WritePositiveInt(40, 14); // 0x646b2c
             bitStream.WritePositiveInt(0, 14); // 0x646b38 */

            /*   bitStream.WritePositiveInt(1000000, 21); // 0xa81858
               bitStream.WriteBoolean(false); // 0xa81868
               bitStream.WritePositiveInt(0, 2); // 0xa81a7c
               bitStream.WriteBoolean(false); // 0xa81a44
               bitStream.WriteBoolean(false); // 0xa81fec
               bitStream.WriteInt(-1, 4); // 0xa81ff8
               bitStream.WriteBoolean(true); // 0xa82040
               bitStream.WriteBoolean(false); // 0xa8204c
               bitStream.WriteBoolean(true); // 0xa82058
               bitStream.WriteBoolean(true); // 0xa82064
               bitStream.WritePositiveInt(0, 5); // 0xa820a8
               bitStream.WritePositiveInt(14, 6); // 0xa820b4
               bitStream.WritePositiveInt(16, 5); // 0xa820c0
               bitStream.WritePositiveInt(32, 6); // 0xa820d0
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa825f4
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa829d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa82b2c
               bitStream.WriteBoolean(true); // 0xa83344
               bitStream.WriteBoolean(false); // 0xa83350
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa83408
               bitStream.WriteBoolean(false); // 0xa83414
               bitStream.WriteBoolean(false); // 0xa83420
               bitStream.WriteBoolean(false); // 0xa8342c
               bitStream.WritePositiveInt(0, 12); // 0xa834b4
               bitStream.WriteBoolean(false); // 0xa834c0
               bitStream.WriteBoolean(false); // 0xa834cc
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa834dc
               bitStream.WriteBoolean(false); // 0xa834f8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa8355c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa8356c
               bitStream.WritePositiveInt(0, 17);
               bitStream.WriteBoolean(false); // 0xa8374c
               bitStream.WriteBoolean(false); // 0xa83798
               bitStream.WritePositiveVInt(27, 4); // 0xa8381c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(57, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(109, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(110, 10); // 0x244f1c
               bitStream.WritePositiveVInt(0, 4); // 0x2381b4
               bitStream.WritePositiveVInt(1, 4); // 0x2381b4
               bitStream.WritePositiveVInt(2, 4); // 0x2381b4
               bitStream.WritePositiveVInt(3, 4); // 0x2381b4
               bitStream.WritePositiveVInt(4, 4); // 0x2381b4
               bitStream.WritePositiveVInt(5, 4); // 0x2381b4
               bitStream.WritePositiveVInt(6, 4); // 0x2381b4
               bitStream.WritePositiveVInt(7, 4); // 0x2381b4
               bitStream.WritePositiveVInt(8, 4); // 0x2381b4
               bitStream.WritePositiveVInt(9, 4); // 0x2381b4
               bitStream.WritePositiveVInt(10, 4); // 0x2381b4
               bitStream.WritePositiveVInt(11, 4); // 0x2381b4
               bitStream.WritePositiveVInt(12, 4); // 0x2381b4
               bitStream.WritePositiveVInt(13, 4); // 0x2381b4
               bitStream.WritePositiveVInt(14, 4); // 0x2381b4
               bitStream.WritePositiveVInt(15, 4); // 0x2381b4
               bitStream.WritePositiveVInt(16, 4); // 0x2381b4
               bitStream.WritePositiveVInt(17, 4); // 0x2381b4
               bitStream.WritePositiveVInt(18, 4); // 0x2381b4
               bitStream.WritePositiveVInt(19, 4); // 0x2381b4
               bitStream.WritePositiveVInt(20, 4); // 0x2381b4
               bitStream.WritePositiveVInt(21, 4); // 0x2381b4
               bitStream.WritePositiveVInt(22, 4); // 0x2381b4
               bitStream.WritePositiveVInt(23, 4); // 0x2381b4
               bitStream.WritePositiveVInt(24, 4); // 0x2381b4
               bitStream.WritePositiveVInt(25, 4); // 0x2381b4
               bitStream.WritePositiveVInt(26, 4); // 0x2381b4
               bitStream.WriteBoolean(false); // 0xa847b0
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(8550, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(0, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WriteBoolean(false); // 0xa1afec
               bitStream.WriteBoolean(false); // 0xa1aff8
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(3600, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(3600, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
               bitStream.WriteBoolean(false); // 0xa1b478
               bitStream.WriteBoolean(false); // 0xa1b4ac
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
               bitStream.WriteBoolean(false); // 0xa1b550
               bitStream.WriteBoolean(false); // 0xa1b5c0
               bitStream.WriteBoolean(false); // 0xa1b5cc
               bitStream.WriteBoolean(false); // 0xa1b660
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b6f4
               bitStream.WriteBoolean(false); // 0xa1b75c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b8b4
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WriteBoolean(false); // 0xa1b9d8
               bitStream.WriteBoolean(false); // 0xa1b9f8
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
               bitStream.WriteBoolean(false); // 0x15a05c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
               bitStream.WritePositiveInt(3000, 12); // 0x15a09c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
               bitStream.WriteBoolean(false); // 0x15a05c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
               bitStream.WritePositiveVInt(750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(7650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(4350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(7650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(9150, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(4350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(9150, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(4050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveInt(100000, 19); // 0xa1b368
               bitStream.WritePositiveInt(100000, 19); // 0xa1b374
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(4650, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveInt(1000000, 21); // 0xa81858
               bitStream.WriteBoolean(false); // 0xa81868
               bitStream.WritePositiveInt(0, 2); // 0xa81a7c
               bitStream.WriteBoolean(false); // 0xa81a44
               bitStream.WriteBoolean(false); // 0xa81fec
               bitStream.WriteInt(-1, 4); // 0xa81ff8
               bitStream.WriteBoolean(true); // 0xa82040
               bitStream.WriteBoolean(false); // 0xa8204c
               bitStream.WriteBoolean(true); // 0xa82058
               bitStream.WriteBoolean(true); // 0xa82064
               bitStream.WritePositiveInt(0, 5); // 0xa820a8
               bitStream.WritePositiveInt(14, 6); // 0xa820b4
               bitStream.WritePositiveInt(16, 5); // 0xa820c0
               bitStream.WritePositiveInt(32, 6); // 0xa820d0
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WriteBoolean(false); // 0xa8218c
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa825f4
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa829d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa82b2c
               bitStream.WriteBoolean(true); // 0xa83344
               bitStream.WriteBoolean(false); // 0xa83350
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa83408
               bitStream.WriteBoolean(false); // 0xa83414
               bitStream.WriteBoolean(false); // 0xa83420
               bitStream.WriteBoolean(false); // 0xa8342c
               bitStream.WritePositiveInt(0, 12); // 0xa834b4
               bitStream.WriteBoolean(false); // 0xa834c0
               bitStream.WriteBoolean(false); // 0xa834cc
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa834dc
               bitStream.WriteBoolean(false); // 0xa834f8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa8355c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa8356c
               bitStream.WriteBoolean(false); // 0xa8374c
               bitStream.WriteBoolean(false); // 0xa83798
               bitStream.WritePositiveVInt(27, 4); // 0xa8381c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(57, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(108, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(107, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(109, 10); // 0x244f1c
               bitStream.WritePositiveInt(16, 5); // 0x244f08
               bitStream.WritePositiveInt(110, 10); // 0x244f1c
               bitStream.WritePositiveVInt(0, 4); // 0x2381b4
               bitStream.WritePositiveVInt(1, 4); // 0x2381b4
               bitStream.WritePositiveVInt(2, 4); // 0x2381b4
               bitStream.WritePositiveVInt(3, 4); // 0x2381b4
               bitStream.WritePositiveVInt(4, 4); // 0x2381b4
               bitStream.WritePositiveVInt(5, 4); // 0x2381b4
               bitStream.WritePositiveVInt(6, 4); // 0x2381b4
               bitStream.WritePositiveVInt(7, 4); // 0x2381b4
               bitStream.WritePositiveVInt(8, 4); // 0x2381b4
               bitStream.WritePositiveVInt(9, 4); // 0x2381b4
               bitStream.WritePositiveVInt(10, 4); // 0x2381b4
               bitStream.WritePositiveVInt(11, 4); // 0x2381b4
               bitStream.WritePositiveVInt(12, 4); // 0x2381b4
               bitStream.WritePositiveVInt(13, 4); // 0x2381b4
               bitStream.WritePositiveVInt(14, 4); // 0x2381b4
               bitStream.WritePositiveVInt(15, 4); // 0x2381b4
               bitStream.WritePositiveVInt(16, 4); // 0x2381b4
               bitStream.WritePositiveVInt(17, 4); // 0x2381b4
               bitStream.WritePositiveVInt(18, 4); // 0x2381b4
               bitStream.WritePositiveVInt(19, 4); // 0x2381b4
               bitStream.WritePositiveVInt(20, 4); // 0x2381b4
               bitStream.WritePositiveVInt(21, 4); // 0x2381b4
               bitStream.WritePositiveVInt(22, 4); // 0x2381b4
               bitStream.WritePositiveVInt(23, 4); // 0x2381b4
               bitStream.WritePositiveVInt(24, 4); // 0x2381b4
               bitStream.WritePositiveVInt(25, 4); // 0x2381b4
               bitStream.WritePositiveVInt(26, 4); // 0x2381b4
               bitStream.WriteBoolean(false); // 0xa847b0
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(8550, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(0, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WriteBoolean(false); // 0xa1afec
               bitStream.WriteBoolean(false); // 0xa1aff8
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(3600, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(3600, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b454
               bitStream.WriteBoolean(false); // 0xa1b478
               bitStream.WriteBoolean(false); // 0xa1b4ac
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b53c
               bitStream.WriteBoolean(false); // 0xa1b550
               bitStream.WriteBoolean(false); // 0xa1b5c0
               bitStream.WriteBoolean(false); // 0xa1b5cc
               bitStream.WriteBoolean(false); // 0xa1b660
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b6f4
               bitStream.WriteBoolean(false); // 0xa1b75c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b8b4
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WriteBoolean(false); // 0xa1b9d8
               bitStream.WriteBoolean(false); // 0xa1b9f8
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
               bitStream.WriteBoolean(false); // 0x15a05c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
               bitStream.WritePositiveInt(3000, 12); // 0x15a09c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a048
               bitStream.WriteBoolean(false); // 0x15a05c
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0x15a068
               bitStream.WritePositiveVInt(750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(7650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(4350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(7650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(9150, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(4350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(9150, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(450, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(1650, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1350, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(1950, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(2550, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3150, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(2250, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(1500, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(750, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(4050, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveInt(100000, 19); // 0xa1b368
               bitStream.WritePositiveInt(100000, 19); // 0xa1b374
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10
               bitStream.WritePositiveVInt(4650, 4); // 0x8bb9e0
               bitStream.WritePositiveVInt(3750, 4); // 0x8bb9ec
               bitStream.WritePositiveVInt(0, 4); // 0x8bb9f8
               bitStream.WritePositiveVInt(17, 3); // 0x8bba04
               bitStream.WritePositiveInt(10, 4); // 0x8bba50
               bitStream.WritePositiveInt(90, 9); // 0xa1b018
               bitStream.WritePositiveInt(90, 9); // 0xa1b024
               bitStream.WritePositiveInt(4, 3); // 0xa1b034
               bitStream.WriteBoolean(false); // 0xa1b054
               bitStream.WriteInt(63, 6); // 0xa1b060
               bitStream.WriteBoolean(false); // 0xa1b06c
               bitStream.WriteBoolean(false); // 0xa1b078
               bitStream.WriteBoolean(false); // 0xa1b09c
               bitStream.WriteBoolean(false); // 0xa1b0a8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0d8
               bitStream.WritePositiveVIntOftenZero(0, 4); // 0xa1b0e4
               bitStream.WriteBoolean(false); // 0xa1b0f0
               bitStream.WriteBoolean(false); // 0xa1b0fc
               bitStream.WriteBoolean(false); // 0xa1b108
               bitStream.WriteBoolean(false); // 0xa1b114
               bitStream.WriteBoolean(false); // 0xa1b120
               bitStream.WriteBoolean(false); // 0xa1b12c
               bitStream.WriteBoolean(false); // 0xa1b154
               bitStream.WriteBoolean(false); // 0xa1b160
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b184
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b190
               bitStream.WriteBoolean(false); // 0xa1b308
               bitStream.WritePositiveVIntMax255OftenZero(0); // 0xa1b314
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b39c
               bitStream.WritePositiveVInt(4000, 4); // 0xa1b3a8
               bitStream.WriteBoolean(false); // 0xa1b3e8
               bitStream.WriteBoolean(false); // 0xa1b9a0
               bitStream.WritePositiveInt(0, 2); // 0xa1b9ac
               bitStream.WriteBoolean(false); // 0xa1b9b8
               bitStream.WritePositiveInt(0, 9); // 0xa1b9c4
               bitStream.WritePositiveInt(0, 5); // 0xa1ba10 */
        }
    }
}
