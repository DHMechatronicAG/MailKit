﻿//
// SaslMechanismDigestMd5Tests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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

using System.Net;
using System.Text;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismDigestMd5Tests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("smtp://localhost");

			var sasl = new SaslMechanismDigestMd5 (credentials) { Uri = uri };
			Assert.Throws<NotSupportedException> (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 ("username", null));
		}

		static void AssertExampleFromRfc2831 (SaslMechanismDigestMd5 sasl, string prefix)
		{
			const string serverToken1 = "realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8";
			const string expected1 = "username=\"chris\",realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"OA6MHXh6VqTrRk\",nc=00000001,qop=\"auth\",digest-uri=\"imap/elwood.innosoft.com\",response=d388dad90d4bbd760a152321f2143af7,charset=utf-8,algorithm=md5-sess";
			const string serverToken2 = "rspauth=ea40f60335c427b5527b84dbabcdfffd";
			const string entropy = "OA6MHXh6VqTrRk";
			string challenge, result;
			byte[] token, decoded;

			sasl.cnonce = entropy;

			Assert.That (sasl.SupportsChannelBinding, Is.False, $"{prefix}: SupportsChannelBinding");
			Assert.That (sasl.SupportsInitialResponse, Is.False, $"{prefix}: SupportsInitialResponse");

			token = Encoding.ASCII.GetBytes (serverToken1);
			challenge = sasl.Challenge (Convert.ToBase64String (token));
			decoded = Convert.FromBase64String (challenge);
			result = Encoding.ASCII.GetString (decoded);

			Assert.That (result, Is.EqualTo (expected1), $"{prefix}: challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, $"{prefix}: should not be authenticated yet.");

			token = Encoding.ASCII.GetBytes (serverToken2);
			challenge = sasl.Challenge (Convert.ToBase64String (token));
			decoded = Convert.FromBase64String (challenge);
			result = Encoding.ASCII.GetString (decoded);

			Assert.That (result, Is.EqualTo (string.Empty), $"{prefix}: second DIGEST-MD5 challenge should be an empty string.");
			Assert.That (sasl.IsAuthenticated, Is.True, $"{prefix}: should be authenticated now.");
			Assert.That (sasl.NegotiatedChannelBinding, Is.False, $"{prefix}: NegotiatedChannelBinding");
			Assert.That (sasl.NegotiatedSecurityLayer, Is.False, $"{prefix}: NegotiatedSecurityLayer");

			Assert.That (sasl.Challenge (string.Empty), Is.EqualTo (string.Empty), $"{prefix}: challenge while authenticated.");
		}

		[Test]
		public void TestExampleFromRfc2831 ()
		{
			var credentials = new NetworkCredential ("chris", "secret");
			var uri = new Uri ("imap://elwood.innosoft.com");
			var sasl = new SaslMechanismDigestMd5 (credentials) { Uri = uri };

			AssertExampleFromRfc2831 (sasl, "NetworkCredential");

			sasl = new SaslMechanismDigestMd5 ("chris", "secret") { Uri = uri };

			AssertExampleFromRfc2831 (sasl, "user/pass");
		}

		static void AssertSaslException (SaslMechanismDigestMd5 sasl, string challenge, SaslErrorCode code)
		{
			var token = Encoding.ASCII.GetBytes (challenge);

			try {
				sasl.Challenge (Convert.ToBase64String (token));
			} catch (SaslException sex) {
				Assert.That (sex.ErrorCode, Is.EqualTo (code), "ErrorCode");
				return;
			} catch (Exception ex) {
				Assert.Fail ($"SaslException expected, but got: {ex.GetType ().Name}");
				return;
			}

			Assert.Fail ("SaslException expected.");
		}

		[Test]
		public void TestSaslExceptions ()
		{
			const string serverToken1 = "realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8,cipher=\"des,3des,rc4\"";
			const string expected1 = "username=\"chris\",realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"OA6MHXh6VqTrRk\",nc=00000001,qop=\"auth\",digest-uri=\"imap/elwood.innosoft.com\",response=d388dad90d4bbd760a152321f2143af7,charset=utf-8,algorithm=md5-sess";
			const string serverToken2 = "rspauth=ea40f60335c427b5527b84dbabcdfffd";
			const string entropy = "OA6MHXh6VqTrRk";
			string challenge, result;
			byte[] token, decoded;

			var uri = new Uri ("imap://elwood.innosoft.com");
			var sasl = new SaslMechanismDigestMd5 ("chris", "secret") { Uri = uri, cnonce = entropy };

			AssertSaslException (sasl, serverToken1 + ",xyz=" + new string ('x', 2048), SaslErrorCode.ChallengeTooLong);
			AssertSaslException (sasl, "username = \"domain\\chris", SaslErrorCode.InvalidChallenge); // incomplete quoted value
			AssertSaslException (sasl, serverToken1 + ",xyz", SaslErrorCode.InvalidChallenge); // missing '='
			AssertSaslException (sasl, serverToken1 + ",xyz=", SaslErrorCode.InvalidChallenge); // incomplete value
			AssertSaslException (sasl, serverToken1 + ",maxbuf=xyz", SaslErrorCode.InvalidChallenge); // invalid maxbuf value
			AssertSaslException (sasl, serverToken1 + ",nonce=\"OA6MG9tEQGm2hh\"", SaslErrorCode.InvalidChallenge); // multiple nonce
			AssertSaslException (sasl, serverToken1 + ",stale=false,stale=true", SaslErrorCode.InvalidChallenge); // multiple stale
			AssertSaslException (sasl, serverToken1 + ",maxbuf=1024,maxbuf=512", SaslErrorCode.InvalidChallenge); // multiple maxbuf
			AssertSaslException (sasl, serverToken1 + ",charset=iso-8859-1", SaslErrorCode.InvalidChallenge); // multiple charset
			AssertSaslException (sasl, serverToken1 + ",algorithm=md5-sess", SaslErrorCode.InvalidChallenge); // multiple algorithm
			AssertSaslException (sasl, serverToken1 + ",cipher=\"des,3des\"", SaslErrorCode.InvalidChallenge); // multiple ciphers

			token = Encoding.ASCII.GetBytes (serverToken1);
			challenge = sasl.Challenge (Convert.ToBase64String (token));
			decoded = Convert.FromBase64String (challenge);
			result = Encoding.ASCII.GetString (decoded);

			Assert.That (result, Is.EqualTo (expected1), "challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, "should not be authenticated yet.");

			AssertSaslException (sasl, string.Empty, SaslErrorCode.MissingChallenge);
			AssertSaslException (sasl, "rspauth", SaslErrorCode.IncompleteChallenge);
			AssertSaslException (sasl, "maxbuf=123", SaslErrorCode.InvalidChallenge);
			AssertSaslException (sasl, "rspauth=fffffffffffffffffffffffffffffff", SaslErrorCode.IncorrectHash);

			token = Encoding.ASCII.GetBytes (serverToken2);
			challenge = sasl.Challenge (Convert.ToBase64String (token));
			decoded = Convert.FromBase64String (challenge);
			result = Encoding.ASCII.GetString (decoded);

			Assert.That (result, Is.EqualTo (string.Empty), "second DIGEST-MD5 challenge should be an empty string.");
			Assert.That (sasl.IsAuthenticated, Is.True, "should be authenticated now.");
			Assert.That (sasl.Challenge (string.Empty), Is.EqualTo (string.Empty), "challenge while authenticated.");
		}
	}
}
