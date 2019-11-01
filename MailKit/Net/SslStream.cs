﻿//
// SslStream.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2019 Xamarin Inc. (www.xamarin.com)
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

using System.IO;
using System.Threading;
using System.Net.Security;
using System.Threading.Tasks;

namespace MailKit.Net
{
	class SslStream : System.Net.Security.SslStream
	{
		public SslStream (Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback) : base (innerStream, leaveInnerStreamOpen, userCertificateValidationCallback)
		{
		}

		new public Stream InnerStream {
			get { return base.InnerStream; }
		}

		public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (cancellationToken.CanBeCanceled && InnerStream is NetworkStream network) {
				cancellationToken.ThrowIfCancellationRequested ();

				await network.PollReadAsync (cancellationToken).ConfigureAwait (false);
			}

			return await base.ReadAsync (buffer, offset, count, cancellationToken).ConfigureAwait (false);
		}
	}
}
