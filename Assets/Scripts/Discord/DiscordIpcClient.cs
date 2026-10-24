using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DLS.Discord
{
	public sealed class DiscordIpcClient : IDisposable
	{
		const int OpcodeHandshake = 0;
		const int OpcodeFrame = 1;
		const int OpcodeClose = 2;
		const int OpcodePing = 3;
		const int OpcodePong = 4;
		const int MaxPayloadBytes = 1024 * 1024;

		readonly string clientId;
		readonly int processId;
		readonly AutoResetEvent wake = new(false);
		readonly object stateLock = new();
		readonly object writeLock = new();

		Thread workerThread;
		volatile Thread readerThread;
		volatile Stream stream;
		volatile bool ready;
		volatile bool running;
		bool disposed;
		string currentActivity;
		bool clearRequested;

		public DiscordIpcClient(string clientId)
		{
			this.clientId = clientId;

			try
			{
				processId = Process.GetCurrentProcess().Id;
			}
			catch (Exception)
			{
				processId = 0;
			}
		}

		public bool IsConnected => ready && stream != null;

		public void Start()
		{
			if (disposed || workerThread != null) return;

			running = true;
			workerThread = new Thread(Worker)
			{
				IsBackground = true,
				Name = "Discord IPC"
			};
			workerThread.Start();
		}

		public void SetActivity(string activityJson)
		{
			lock (stateLock)
			{
				currentActivity = activityJson;
				clearRequested = false;
			}

			wake.Set();
		}

		public void ClearActivity()
		{
			lock (stateLock)
			{
				currentActivity = null;
				clearRequested = true;
			}

			wake.Set();
		}

		void Worker()
		{
			while (running)
			{
				Stream connection = null;

				try
				{
					connection = TryConnect();
					if (connection == null)
					{
						SleepRetry();
						continue;
					}

					ready = false;
					stream = connection;
					WriteFrame(connection, OpcodeHandshake, "{\"v\":1,\"client_id\":\"" + clientId + "\"}");

					Thread reader = new(() => ReadLoop(connection))
					{
						IsBackground = true,
						Name = "Discord IPC Reader"
					};
					readerThread = reader;
					reader.Start();

					bool sentSinceReady = false;

					while (running && ReferenceEquals(stream, connection))
					{
						bool signaled = wake.WaitOne(1000);
						if (!running || !ReferenceEquals(stream, connection)) break;
						if (!ready) continue;
						if (!signaled && sentSinceReady) continue;

						if (SendCurrent(connection)) sentSinceReady = true;
					}
				}
				catch (Exception)
				{
				}
				finally
				{
					Disconnect(connection);
				}

				if (running) SleepRetry();
			}
		}

		bool SendCurrent(Stream connection)
		{
			bool clear;
			string activity;

			lock (stateLock)
			{
				clear = clearRequested;
				clearRequested = false;
				activity = currentActivity;
			}

			if (clear)
			{
				WriteCommand(connection, null);
				return true;
			}

			if (activity == null) return false;

			WriteCommand(connection, activity);
			return true;
		}

		void WriteCommand(Stream connection, string activityJson)
		{
			string args = activityJson == null
				? "{\"pid\":" + processId + ",\"activity\":null}"
				: "{\"pid\":" + processId + ",\"activity\":" + activityJson + "}";

			string command = "{\"cmd\":\"SET_ACTIVITY\",\"args\":" + args + ",\"nonce\":\"" + Guid.NewGuid() + "\"}";
			WriteFrame(connection, OpcodeFrame, command);
		}

		void ReadLoop(Stream connection)
		{
			byte[] header = new byte[8];

			try
			{
				while (running && ReferenceEquals(stream, connection))
				{
					ReadExact(connection, header, header.Length);

					int opcode = ReadInt32(header, 0);
					int length = ReadInt32(header, 4);
					if (length < 0 || length > MaxPayloadBytes) throw new IOException("Invalid Discord IPC payload length.");

					byte[] payload = new byte[length];
					if (length > 0) ReadExact(connection, payload, length);

					if (opcode == OpcodeFrame)
					{
						if (!ready)
						{
							ready = true;
							wake.Set();
						}
					}
					else if (opcode == OpcodeClose)
					{
						return;
					}
					else if (opcode == OpcodePing)
					{
						WriteFrame(connection, OpcodePong, Encoding.UTF8.GetString(payload));
					}
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				Disconnect(connection);
			}
		}

		void Disconnect(Stream connection)
		{
			ready = false;

			if (connection != null && ReferenceEquals(stream, connection))
			{
				stream = null;
			}

			try
			{
				connection?.Dispose();
			}
			catch (Exception)
			{
			}

			wake.Set();
		}

		void SleepRetry()
		{
			for (int i = 0; i < 50 && running; i++)
			{
				Thread.Sleep(100);
			}
		}

		static Stream TryConnect()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) return TryConnectWindows();
			return TryConnectUnix();
		}

		static Stream TryConnectWindows()
		{
			for (int i = 0; i < 10; i++)
			{
				NamedPipeClientStream pipe = null;

				try
				{
					pipe = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.Asynchronous);
					pipe.Connect(50);

					if (pipe.IsConnected) return pipe;
				}
				catch (Exception)
				{
				}

				try
				{
					pipe?.Dispose();
				}
				catch (Exception)
				{
				}
			}

			return null;
		}

		static Stream TryConnectUnix()
		{
			foreach (string path in EnumerateUnixPaths())
			{
				Socket socket = null;

				try
				{
					socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
					socket.Connect(new UnixDomainSocketEndPoint(path));
					return new NetworkStream(socket, true);
				}
				catch (Exception)
				{
					try
					{
						socket?.Dispose();
					}
					catch (Exception)
					{
					}
				}
			}

			return null;
		}

		static IEnumerable<string> EnumerateUnixPaths()
		{
			string[] roots =
			{
				Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
				Environment.GetEnvironmentVariable("TMPDIR"),
				Environment.GetEnvironmentVariable("TMP"),
				Environment.GetEnvironmentVariable("TEMP"),
				"/tmp"
			};

			string[] suffixes =
			{
				string.Empty,
				"app/com.discordapp.Discord",
				"snap.discord",
				".flatpak/dev.vencord.Vesktop/xdg-run"
			};

			HashSet<string> seen = new(StringComparer.Ordinal);

			foreach (string root in roots)
			{
				if (string.IsNullOrWhiteSpace(root)) continue;

				for (int i = 0; i < 10; i++)
				{
					string fileName = "discord-ipc-" + i;

					foreach (string suffix in suffixes)
					{
						string path = suffix.Length == 0
							? Path.Combine(root, fileName)
							: Path.Combine(root, suffix, fileName);

						if (seen.Add(path))
						{
							yield return path;
						}
					}
				}
			}
		}

		static void ReadExact(Stream connection, byte[] buffer, int count)
		{
			int offset = 0;

			while (offset < count)
			{
				int bytesRead = connection.Read(buffer, offset, count - offset);
				if (bytesRead <= 0) throw new EndOfStreamException();
				offset += bytesRead;
			}
		}

		static int ReadInt32(byte[] buffer, int offset)
		{
			return buffer[offset]
				| (buffer[offset + 1] << 8)
				| (buffer[offset + 2] << 16)
				| (buffer[offset + 3] << 24);
		}

		static void WriteInt32(byte[] buffer, int offset, int value)
		{
			buffer[offset] = (byte)value;
			buffer[offset + 1] = (byte)(value >> 8);
			buffer[offset + 2] = (byte)(value >> 16);
			buffer[offset + 3] = (byte)(value >> 24);
		}

		void WriteFrame(Stream connection, int opcode, string json)
		{
			byte[] payload = Encoding.UTF8.GetBytes(json);
			byte[] frame = new byte[8 + payload.Length];

			WriteInt32(frame, 0, opcode);
			WriteInt32(frame, 4, payload.Length);
			Buffer.BlockCopy(payload, 0, frame, 8, payload.Length);

			lock (writeLock)
			{
				connection.Write(frame, 0, frame.Length);
				connection.Flush();
			}
		}

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;

			Stream connection = stream;

			if (connection != null && ready)
			{
				try
				{
					WriteCommand(connection, null);
				}
				catch (Exception)
				{
				}
			}

			running = false;
			wake.Set();

			if (connection != null)
			{
				try
				{
					connection.Dispose();
				}
				catch (Exception)
				{
				}
			}

			if (workerThread != null)
			{
				try
				{
					workerThread.Join(1000);
				}
				catch (Exception)
				{
				}
			}

			Thread reader = readerThread;
			if (reader != null && reader != Thread.CurrentThread)
			{
				try
				{
					reader.Join(500);
				}
				catch (Exception)
				{
				}
			}

			GC.SuppressFinalize(this);
		}
	}
}
