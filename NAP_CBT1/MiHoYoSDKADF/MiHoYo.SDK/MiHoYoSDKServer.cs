using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Il2CppDummyDll;
using UnityEngine;

namespace MiHoYo.SDK
{
	public class MiHoYoSDKServer : MonoBehaviour
	{
		public static MiHoYoSDKServer Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject();
					go.name = "MiHoYoSDKServer";
					_instance = go.AddComponent<MiHoYoSDKServer>();
					UnityEngine.Object.DontDestroyOnLoad(go);
					SecurityTunnel.FileTransferDirectory = Application.persistentDataPath;
					ThreadDispatcher.Instance?.RunOnMainThread(() => { });
					_instance?.KibanaReport("get_Instance", null);
				}
				return _instance;
			}
			private set
			{
				_instance = value;
			}
		}

		private void OnDestroy()
		{
		}

		public event OnConnectDelegate OnConnectResult;

		public event SecurityTunnel.OnConnectedAction OnConnected;

		public event SecurityTunnel.OnDisconnectedAction OnDisconnected;

		public event SecurityTunnel.OnSDKEventCallbackAction OnSDKEventCallback;

		public event SecurityTunnel.OnServerEventCallbackAction OnServerEventCallback;

		public event SecurityTunnel.ResponseCallbackAction OnGetMessage;

		public event SecurityTunnel.OnServerKickOffCallbackAction OnServerKickOff;

		public event SecurityTunnel.OnServerShutdownCallbackAction OnServerShutdown;

		public event SecurityTunnel.OnLogVerboseAction OnLogVerbose;

		public event SecurityTunnel.OnLogMessageAction OnLogMessage;

		public event SecurityTunnel.OnLogErrorAction OnLogError;

		public event SecurityTunnel.OnLogErrorWithCodeAction OnLogErrorWithCode;

		public void Connect()
		{
			KibanaReport("Connect", null);
			GetGateAddress();
		}

		public void SendMessage2Server(int evt, byte[] bytes)
		{
			if (tunnel != null)
			{
				KibanaReport("SendMessage2Server", "{}");
				tunnel.SendMessage2Server(evt, bytes);
			}
		}

		public void SendMessage2SDK(int evt, byte[] bytes)
		{
			if (tunnel != null)
			{
				KibanaReport("SendMessage2SDK", "{}");
				tunnel.SendMessage2SDK(evt, bytes);
			}
		}

		public void SendMessage(SecurityTunnel.PacketFlag packetFlag, SecurityTunnel.PacketCommand secureMessage, string body)
		{
			if (tunnel != null)
			{
				KibanaReport("SendMessage", "");
				SecurityTunnel.PacketHeader header = new SecurityTunnel.PacketHeader(packetFlag, secureMessage, 0u);
				SecurityTunnel.Packet packet = new SecurityTunnel.Packet(header, body);
				tunnel.Write(packet, (bool isSuccess, SecurityTunnel.Packet responsePacket) => { });
			}
		}

		public void SendMessage(SecurityTunnel.PacketFlag packetFlag, SecurityTunnel.PacketCommand secureMessage, byte[] body)
		{
			if (tunnel != null)
			{
				KibanaReport("SendMessage", "{}");
				SecurityTunnel.PacketHeader header = new SecurityTunnel.PacketHeader(packetFlag, secureMessage, 0u);
				SecurityTunnel.Packet packet = new SecurityTunnel.Packet(header, body);
				tunnel.Write(packet, (bool isSuccess, SecurityTunnel.Packet responsePacket) => { });
			}
		}

		public void SendMessage(MemoryStream memoryStream)
		{
			if (tunnel != null)
			{
				KibanaReport("SendMessage", "MemoryStream");
				SecurityTunnel.Packet packet = new SecurityTunnel.Packet(memoryStream);
				tunnel.Write(packet, (bool isSuccess, SecurityTunnel.Packet responsePacket) => { });
			}
		}

		public void Disconnect()
		{
			if (tunnel != null)
			{
				KibanaReport("Disconnect", null);
				tunnel.Disconnect();
				tunnel = null;
			}
		}

		public void TryLogout()
		{
			if (tunnel != null)
			{
				KibanaReport("TryLogout", null);
				tunnel.TryLogout();
			}
		}

		public void Invoke(string funcName, [Optional] string args, int index = -1)
		{
			if (funcName == "setGameRole" || funcName == "setRole")
			{
				if (!string.IsNullOrEmpty(args))
				{
					MiHoYoSDKGameRoleModel role = JsonUtility.FromJson<MiHoYoSDKGameRoleModel>(args);
					if (role != null)
					{
						KibanaReport("SetGameRole", args);
						gameRole = role;
					}
				}
			}
			else if (funcName == "setGameParameters")
			{
				SetGameParameters(args);
			}
			else if (funcName == "login")
			{
				loginCallbackIndex = index;
			}
			else if (funcName == "setEnv")
			{
				SetEnv(args);
			}
			else if (funcName == "setConfig")
			{
				if (!string.IsNullOrEmpty(args))
				{
					JSONNode json = JSON.Parse(args);
					if (json != null && json["env"] != null)
					{
						JSONNode envNode = json["env"];
						if (envNode != null)
						{
							string envStr = envNode;
							SetEnv(envStr);
						}
					}
					SetGameParameters(args);
				}
			}
		}

		public void SetSDKInit()
		{
			isInit = true;
		}

		public void InvokeCallback(int index, string data)
		{
			if (loginCallbackIndex == index && index != -1)
			{
				SetLoginResult(data);
			}
		}

		public static void SetLoginResult(string dataString)
		{
			if (string.IsNullOrEmpty(dataString))
			{
				loginResult = null;
				return;
			}

			Instance?.KibanaReport("SetLoginResult", dataString);
			JSONNode json = JSON.Parse(dataString);
			if (json != null && json["retcode"] != null)
			{
				int retcode = json["retcode"].AsInt;
				if (retcode == 0)
				{
					JSONNode data = json["data"];
					loginResult = new LoginResultModel(data);
				}
				else
				{
					loginResult = null;
				}
			}
		}

		public void TestErrorLog()
		{
		}

		private void SetEnv(string args)
		{
			if (!string.IsNullOrEmpty(args))
			{
				KibanaReport("SetEnv", args);
				try
				{
					env = (EnvType)Enum.Parse(typeof(EnvType), args);
				}
				catch
				{
					env = EnvType.DEFAULT;
				}
			}
		}

		private string GetGateAddressURL()
		{
			string gameBiz = gameParameter?.gameBiz ?? "";
			string serverId = gameRole?.server_id ?? "";
			string url = "";

			switch (env)
			{
				case EnvType.DEFAULT:
				case EnvType.RC:
					url = string.Format("https://sdk-{0}-static.mihoyo.com/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}", gameBiz, serverId).Replace('_', '-');
					break;
				case EnvType.DEV:
					url = "https://dev-sdk-static.mihoyo.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.TEST1:
					url = "https://test1-sdk-static.mihoyo.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.MIYOUSHE_PRE:
					url = "https://pre-sdk-static.miyoushe.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.MIYOUSHE:
					url = "https://sdk-static.miyoushe.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.HOYOVERSE_PRE:
					url = "https://pre-sdk-os-static.hoyoverse.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.OSA1:
					url = "https://osa1-sdk-os-static.hoyoverse.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.OSA2:
					url = "https://osa2-sdk-os-static.hoyoverse.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.SG:
					url = "https://sg-sdk-os-static.hoyoverse.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				case EnvType.HOYOVERSE:
					url = "https://sdk-os-static.hoyoverse.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
				default:
					url = "https://sdk-static.mihoyo.com" + "/combo/granter/api/getConfig?channel=1&game_biz={0}&server_id={1}";
					break;
			}
			return string.Format(url, gameBiz, serverId);
		}

		private string GetPublicKey()
		{
			switch (env)
			{
				case EnvType.OSA1:
				case EnvType.OSA2:
					return "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQC+h6vgCnLUKr5lFSHdT3K8aY3YKC8cVhLsL9wZF2+d7e7NqD3wPXLJvQQdYvPEf4jVKJ7qPkS5xZ7yB6NjxC0lEiZnSZ7gHJzWKJ7vCwZ6tN0Jw7ZCr4jQkCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCZKpCQIDAQAB";
				default:
					return "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDDvekdPMHN3AYhm/vktJT+YJr7cI5DcsNKqdsx5DZZkykFYLUntLy5K0B5e9uLRHWTNMlYU1x0wYYWWVaqYqSUVrhCJ3YJqCQKLrchjdxEWCZDROYKSPGfJmD6h2jlmONqKzYhMH6Fq5p6/rWOg0KkGY5O/6VIXJjcHPEI1QIDAQAB";
			}
		}

		public string GetKibanaReportURL()
		{
			switch (env)
			{
				case EnvType.DEV:
				case EnvType.TEST1:
				case EnvType.OSA1:
				case EnvType.OSA2:
					return "https://sdk-static.mihoyo.com" + "/hk4e_cn/combo/panda/config/sdk_report";
				case EnvType.RC:
				case EnvType.MIYOUSHE:
				case EnvType.GAMECLOUD:
					return "https://public-data-api.mihoyo.com" + "/hk4e_cn/combo/panda/config/sdk_report";
				case EnvType.MIYOUSHE_PRE:
					return "https://public-data-api-pre.miyoushe.com" + "/hk4e_cn/combo/panda/config/sdk_report";
				case EnvType.HOYOVERSE_PRE:
					return "https://public-data-api-os-pre.hoyoverse.com" + "/hk4e_cn/combo/panda/config/sdk_report";
				default:
					return "https://public-data-api.mihoyo.com" + "/hk4e_cn/combo/panda/config/sdk_report";
			}
		}

		private void SetGameRole(string args)
		{
			if (!string.IsNullOrEmpty(args))
			{
				MiHoYoSDKGameRoleModel role = JsonUtility.FromJson<MiHoYoSDKGameRoleModel>(args);
				if (role != null)
				{
					KibanaReport("SetGameRole", args);
					gameRole = role;
				}
			}
		}

		private void SetGameParameters(string dataString)
		{
			gameParameter = new GameParameterModel(dataString);
			KibanaReport("SetGameParameters", dataString ?? "");
		}

		private void GetGateAddress()
		{
			if (gameRole != null && !string.IsNullOrEmpty(gameRole.server_id))
			{
				KibanaReport("GetGateAddress", null);
				string url = GetGateAddressURL();
				GetRequest(url, null, (NetworkResponseModel model) =>
				{
					if (model.retcode == 0 && model.data != null)
					{
						JSONNode addressList = model.data["combo_token_infos"];
						if (addressList != null)
						{
							StartSercurityTunnel(addressList);
						}
					}
				});
			}
		}

		private void OnGetConnectResult(int retCode, string message)
		{
			if (OnConnectResult != null)
			{
				string msg = "OnGetConnectResult: " + retCode + " message: " + message;
				KibanaReport("OnGetConnectResult", msg);
				OnConnectResult(retCode, message);
			}
		}

		private void StartSercurityTunnel(JSONNode addressList)
		{
			if (addressList != null)
			{
				List<SecurityTunnel.ServerAddress> addresses = new List<SecurityTunnel.ServerAddress>();
				foreach (JSONNode addr in addressList.AsArray)
				{
					SecurityTunnel.ServerAddress sa = new SecurityTunnel.ServerAddress
					{
						Host = addr["ip"],
						Port = addr["port"].AsInt
					};
					addresses.Add(sa);
				}

				if (tunnel == null)
				{
					tunnel = new SecurityTunnel();
				}

				RegisterEvent();

				tunnel.publicKey = GetPublicKey();
				tunnel.Connect(addresses, OnGetConnectResult);
			}
		}

		private void RegisterEvent()
		{
			if (tunnel != null)
			{
				tunnel.OnConnectedCallback += OnGetConnected;
				tunnel.OnDisconnectedCallback += OnGetDisconnected;
				tunnel.OnLogVerboseCallback += OnGetLogVerbose;
				tunnel.OnLogMessageCallback += OnGetLogMessage;
				tunnel.OnLogErrorCallback += OnGetLogError;
				tunnel.OnLogErrorWithCodeCallback += OnGetLogErrorWithCode;
				tunnel.OnSDKEventCallback += OnGetSDKEventCallback;
				tunnel.OnServerEventCallback += OnGetServerEventCallback;
				tunnel.ResponseCallback += OnMessageResponse;
				tunnel.OnServerKickOffCallback += OnGetServerKickOff;
				tunnel.OnServerShutdownCallback += OnGetServerShutdown;
			}
		}

		private void UnregisterEvent()
		{
			if (tunnel != null)
			{
				tunnel.OnConnectedCallback -= OnGetConnected;
				tunnel.OnDisconnectedCallback -= OnGetDisconnected;
				tunnel.OnLogVerboseCallback -= OnGetLogVerbose;
				tunnel.OnLogMessageCallback -= OnGetLogMessage;
				tunnel.OnLogErrorCallback -= OnGetLogError;
				tunnel.OnLogErrorWithCodeCallback -= OnGetLogErrorWithCode;
				tunnel.OnSDKEventCallback -= OnGetSDKEventCallback;
				tunnel.OnServerEventCallback -= OnGetServerEventCallback;
				tunnel.ResponseCallback -= OnMessageResponse;
				tunnel.OnServerKickOffCallback -= OnGetServerKickOff;
				tunnel.OnServerShutdownCallback -= OnGetServerShutdown;
			}
		}

		private void OnGetConnected()
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				KibanaReport("OnGetConnected", null);
				OnConnected?.Invoke();
			});
		}

		private void OnGetDisconnected()
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				KibanaReport("OnGetDisconnected", null);
				OnDisconnected?.Invoke();
			});
		}

		private void OnGetLogVerbose(string message)
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				OnLogVerbose?.Invoke(message);
			});
		}

		private void OnGetLogMessage(string message)
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				OnLogMessage?.Invoke(message);
			});
		}

		private void OnGetLogError(string message)
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				OnLogError?.Invoke(message);
			});
		}

		private void OnGetLogErrorWithCode(SecurityTunnel.ErrorCode code, string message)
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				OnLogErrorWithCode?.Invoke(code, message);
			});
		}

		private void OnGetSDKEventCallback(int evt, byte[] bytes, uint length)
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				OnSDKEventCallback?.Invoke(evt, bytes, length);
			});
		}

		private void OnGetServerEventCallback(int evt, byte[] bytes, uint length)
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				OnServerEventCallback?.Invoke(evt, bytes, length);
			});
		}

		private void OnMessageResponse(bool isSuccess, SecurityTunnel.Packet packet)
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				OnGetMessage?.Invoke(isSuccess, packet);
			});
		}

		private void OnGetServerKickOff()
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				KibanaReport("OnGetServerKickOff", null);
				OnServerKickOff?.Invoke();
			});
		}

		private void OnGetServerShutdown()
		{
			ThreadDispatcher.Instance?.RunOnMainThread(() =>
			{
				KibanaReport("OnGetServerShutdown", null);
				OnServerShutdown?.Invoke();
			});
		}

		private void KibanaReport(string eventStr, [Optional] string msg)
		{
			JSONObject json = new JSONObject();
			if (!string.IsNullOrEmpty(eventStr))
			{
				json["event"] = eventStr;
			}
			if (!string.IsNullOrEmpty(msg))
			{
				json["msg"] = msg;
			}
		}

		private Dictionary<string, string> SharedHeaders()
		{
			string deviceId = "";
			string appId = "";
			string gameBiz = gameParameter?.gameBiz ?? "";

			if (loginResult != null)
			{
				deviceId = loginResult.deviceId ?? "";
				appId = loginResult.appId.ToString();
			}

			Dictionary<string, string> headers = new Dictionary<string, string>();
			headers["x-rpc-client_type"] = Uri.EscapeDataString(MiHoYoSDKKibana.clientType.ToString());
			headers["x-rpc-sys_version"] = Uri.EscapeDataString(SystemInfo.operatingSystem ?? "");
			headers["x-rpc-channel"] = Uri.EscapeDataString("unknownChannel");
			headers["x-rpc-device_id"] = Uri.EscapeDataString(deviceId);
			headers["x-rpc-device_name"] = Uri.EscapeDataString(SystemInfo.deviceName ?? "");
			headers["x-rpc-device_model"] = Uri.EscapeDataString(SystemInfo.deviceModel ?? "");
			headers["x-rpc-app_version"] = Uri.EscapeDataString(Application.version ?? "");
			headers["x-rpc-sdk_version"] = Uri.EscapeDataString("1.0.0");
			headers["x-rpc-app_id"] = Uri.EscapeDataString(appId);
			headers["x-rpc-game_biz"] = Uri.EscapeDataString(gameBiz);

			return headers;
		}

		private IEnumerator Post(string requestUrl, string bodyString, Action<string> callback, [Optional] Action timeoutCallback, float timeoutSecond = 5f, int retryTime = 3)
		{
			Dictionary<string, string> headers = SharedHeaders();
			return NetUtil.WWWRequestWithRetry(requestUrl, callback, timeoutCallback, bodyString, headers, null, timeoutSecond, retryTime, null);
		}

		public IEnumerator Get(string requestUrl, JSONObject query, Action<string> callback, [Optional] Action timeoutCallback, float timeoutSecond = 5f, int retryTime = 3)
		{
			if (query != null)
			{
				string queryString = MiHoYoSDKUtil.GetQueryString(query);
				requestUrl = requestUrl + queryString;
			}
			Dictionary<string, string> headers = SharedHeaders();
			return NetUtil.WWWRequestWithRetry(requestUrl, callback, timeoutCallback, null, headers, null, timeoutSecond, retryTime, null);
		}

		public void PostRequest(string requestUrl, string bodyString, Action<NetworkResponseModel> callback, float timeoutSecond = 5f, int retryTime = 3)
		{
			StartCoroutine(Post(requestUrl, bodyString, (string responseString) =>
			{
				OnCallback(callback, OnGetCallback(responseString));
			}, () =>
			{
				OnCallback(callback, OnGetTimeOut());
			}, timeoutSecond, retryTime));
		}

		public void GetRequest(string requestUrl, JSONObject query, Action<NetworkResponseModel> callback, float timeoutSecond = 5f, int retryTime = 3)
		{
			StartCoroutine(Get(requestUrl, query, (string responseString) =>
			{
				OnCallback(callback, OnGetCallback(responseString));
			}, () =>
			{
				OnCallback(callback, OnGetTimeOut());
			}, timeoutSecond, retryTime));
		}

		private NetworkResponseModel OnGetCallback(string responseString)
		{
			NetworkResponseModel response = new NetworkResponseModel();
			response.retcode = int.MinValue + 1;
			response.message = "";

			if (!string.IsNullOrEmpty(responseString))
			{
				JSONNode json = JSON.Parse(responseString);
				if (json != null)
				{
					if (json["retcode"] != null)
					{
						response.retcode = json["retcode"].AsInt;
					}
					if (json["message"] != null)
					{
						response.message = json["message"];
					}
					if (json["data"] != null)
					{
						response.data = json["data"];
					}
				}
			}
			return response;
		}

		private NetworkResponseModel OnGetTimeOut()
		{
			NetworkResponseModel response = new NetworkResponseModel();
			response.retcode = int.MinValue;
			response.message = "TimeOut";
			return response;
		}

		private void OnCallback(Action<NetworkResponseModel> callback, NetworkResponseModel response)
		{
			callback?.Invoke(response);
		}

		public MiHoYoSDKServer()
		{
			loginCallbackIndex = -1;
		}

		static MiHoYoSDKServer()
		{
			IPList = new List<string>();
		}

		private static MiHoYoSDKServer _instance;

		public static List<string> IPList;

		public SecurityTunnel tunnel;

		public EnvType env;

		public static LoginResultModel loginResult;

		public MiHoYoSDKGameRoleModel gameRole;

		public GameParameterModel gameParameter;

		public bool isInit;

		private int loginCallbackIndex;

		public const int TimeOutValue = -2147483648;

		public const int ExceptionValue = -2147483647;

		public delegate void OnConnectDelegate(int retCode, string message);

		[Serializable]
		public class NetworkResponseModel
		{
			public NetworkResponseModel()
			{
			}

			public int retcode;

			public string message;

			public JSONNode data;
		}

		[Serializable]
		public class LoginResultModel
		{
			public LoginResultModel(JSONNode json)
			{
				if (json != null)
				{
					if (json["app_id"] != null)
					{
						appId = json["app_id"].AsInt;
					}
					if (json["channel_id"] != null)
					{
						channelId = json["channel_id"].AsInt;
					}
					if (json["account_type"] != null)
					{
						accountType = json["account_type"].AsInt;
					}
					if (json["combo_id"] != null)
					{
						comboId = json["combo_id"].AsInt;
					}
					if (json["open_id"] != null)
					{
						openId = json["open_id"];
					}
					if (json["combo_token"] != null)
					{
						comboToken = json["combo_token"];
					}
					if (json["device_id"] != null)
					{
						deviceId = json["device_id"];
					}
					if (json["guest"] != null)
					{
						guest = json["guest"].AsBool;
					}
					if (json["login_type"] != null)
					{
						loginType = json["login_type"].AsInt;
					}
					if (json["is_new_register"] != null)
					{
						isNewRegister = json["is_new_register"].AsBool;
					}
					if (json["online_id"] != null)
					{
						onlineId = json["online_id"];
					}
					if (json["ps_account_id"] != null)
					{
						psAccountId = json["ps_account_id"];
					}
					if (json["ext"] != null)
					{
						ext = json["ext"];
					}
				}
			}

			public int appId;

			public int channelId;

			public int accountType;

			public int comboId;

			public string openId;

			public string comboToken;

			public string deviceId;

			public bool guest;

			public int loginType;

			public bool isNewRegister;

			public string onlineId;

			public string psAccountId;

			public string ext;
		}

		[Serializable]
		public class GameParameterModel
		{
			public GameParameterModel(string dataString)
			{
				if (!string.IsNullOrEmpty(dataString))
				{
					JSONNode json = JSON.Parse(dataString);
					if (json != null)
					{
						if (json["game"] != null)
						{
							game = json["game"];
						}
						if (json["game_biz"] != null)
						{
							gameBiz = json["game_biz"];
						}
					}
				}
			}

			public string game;

			public string gameBiz;
		}
	}
}