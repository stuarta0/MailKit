﻿//
// SocketUtils.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MailKit.Net
{
	static class SocketUtils
	{
#if NETSTANDARD_2_0 || NET_4_5 || __MOBILE__
		public static async Task<Socket> ConnectAsync (string host, int port, IPEndPoint localEndPoint, bool doAsync, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var socket = new Socket (SocketType.Stream, ProtocolType.Tcp);

			try {
				if (localEndPoint != null)
					socket.Bind (localEndPoint);

				if (doAsync || cancellationToken.CanBeCanceled) {
					var tcs = new TaskCompletionSource<bool> ();

					using (var registration = cancellationToken.Register (() => tcs.SetCanceled (), false)) {
						var ar = socket.BeginConnect (host, port, e => tcs.SetResult (true), null);

						try {
							if (doAsync)
								await tcs.Task.ConfigureAwait (false);
							else
								tcs.Task.Wait ();

							socket.EndConnect (ar);
						} catch (Exception) {
							socket.Close ();
							throw;
						}
					}
				} else {
					socket.Connect (host, port);
				}
			} catch (OperationCanceledException) {
				if (socket.Connected)
					socket.Disconnect (false);

				socket.Dispose ();
				throw;
			} catch {
				socket.Dispose ();
				throw;
			}

			return socket;
		}
#else // .NETStandard 1.3 and 1.6 do not have Socket.BeginConnect()
		public static async Task<Socket> ConnectAsync (string host, int port, IPEndPoint localEndPoint, bool doAsync, CancellationToken cancellationToken)
		{
			IPAddress[] ipAddresses;
			Socket socket = null;

			if (doAsync) {
				ipAddresses = await Dns.GetHostAddressesAsync (host).ConfigureAwait (false);
			} else {
				ipAddresses = Dns.GetHostAddressesAsync (host).GetAwaiter ().GetResult ();
			}

			for (int i = 0; i < ipAddresses.Length; i++) {
				cancellationToken.ThrowIfCancellationRequested ();

				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					if (localEndPoint != null)
						socket.Bind (localEndPoint);

					socket.Connect (ipAddresses[i], port);
				} catch {
					socket.Dispose ();
					socket = null;

					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (socket == null)
				throw new IOException (string.Format ("Failed to resolve host: {0}", host));

			return socket;
		}
#endif

		public static async Task<Socket> ConnectAsync (string host, int port, IPEndPoint localEndPoint, int timeout, bool doAsync, CancellationToken cancellationToken)
		{
			using (var ts = new CancellationTokenSource (timeout)) {
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, ts.Token)) {
					try {
						return await ConnectAsync (host, port, localEndPoint, doAsync, linked.Token).ConfigureAwait (false);
					} catch (OperationCanceledException) {
						if (!cancellationToken.IsCancellationRequested)
							throw new TimeoutException ();
						throw;
					}
				}
			}
		}

		public static void Poll (Socket socket, SelectMode mode, CancellationToken cancellationToken)
		{
			do {
				cancellationToken.ThrowIfCancellationRequested ();
				// wait 1/4 second and then re-check for cancellation
			} while (!socket.Poll (250000, mode));
		}
	}
}
