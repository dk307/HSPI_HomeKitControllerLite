using HomeKit.Exceptions;
using HomeKit.Model;
using HomeKit.Srp;
using NSec.Cryptography;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


#nullable enable

namespace HomeKit
{
    internal sealed record SessionKeys(ImmutableArray<byte> ControllerToAccessoryKey,
                                       ImmutableArray<byte> AccessoryToControllerKey);

    internal sealed class Pairing
    {
        public Pairing(Connection connection)
        {
            this.connection = connection;
        }

        public async Task<SessionKeys> GetSessionKeys(PairingDeviceInfo pairingInfo,
                                                      CancellationToken cancellationToken)
        {
            // Step #1 ios --> accessory (send verify start Request) (page 47)

            using var ios_key = Key.Create(KeyAgreementAlgorithm.X25519);

            var iosKeyPublic = ios_key.Export(KeyBlobFormat.RawPublicKey);

            var tlvDataStep1 = new TlvValue[] {
                new TlvValue( TlvType.State, Tlv8.M1),
                new TlvValue( TlvType.PublicKey, iosKeyPublic),
            };

            var step1ResponseDict = await PostTlv(tlvDataStep1, PairVerifyTarget, string.Empty, cancellationToken)
                                          .ConfigureAwait(false);

            HandlePairingResponse<VerifyPairingException>("Step1", step1ResponseDict, Tlv8.M2);

            // Step #3 ios --> accessory (send SRP verify request)  (page 49)
            var accessorySessionPublicKeyBytes = ConstainsInResponse<PairingException>("Step1", step1ResponseDict, TlvType.PublicKey);
            var encryptedData = ConstainsInResponse<PairingException>("Step1", step1ResponseDict, TlvType.EncryptedData);

            //  1) generate shared secret
            var accessorySessionPublicKey = PublicKey.Import(KeyAgreementAlgorithm.X25519,
                                                              accessorySessionPublicKeyBytes,
                                                              KeyBlobFormat.RawPublicKey);

            var sharedSecret = KeyAgreementAlgorithm.X25519.Agree(ios_key, accessorySessionPublicKey);

            if (sharedSecret == null)
            {
                Log.Error("{step} shared secret cannot be agreed for {Name}", "Step1", connection.DisplayName);
                throw new VerifyPairingException($"Shared Secret cannot be agreed for {connection.DisplayName}");
            }

            // #2) derive session key
            var sessionKey = HkdfDerive(sharedSecret,
                                         "Pair-Verify-Encrypt-Salt",
                                         "Pair-Verify-Encrypt-Info");

            // #3) verify auth tag on encrypted data and 4) decrypt
            if (!ChaChaDecrypt(sessionKey, "PV-Msg02", encryptedData, out var plainData) || plainData == null)
            {
                Log.Error("{step} data cannot be decrypted during verify pair for {Name}", "Step3", connection.DisplayName);
                throw new VerifyPairingException($"Data cannot be decrypted during pair verify for {connection.DisplayName}");
            }

            // #5) look up pairing by accessory name
            var subTlv = Tlv8.Decode(plainData).ToLookup(x => x.Type);

            var accessoryId = ConstainsInResponse<PairingException>("Step5", subTlv, TlvType.Identifier);

            if (!Enumerable.SequenceEqual(pairingInfo.AccessoryPairingId, accessoryId))
            {
                Log.Error("{step} pairing mismatch for during verify pairing for {Name}", "Step5", connection.DisplayName);
                throw new VerifyPairingException($"Pairing Id mismatch for {connection.DisplayName}");
            }

            var ed25519 = SignatureAlgorithm.Ed25519;
            var accessoryPublicKey = PublicKey.Import(ed25519,
                                                  pairingInfo.AccessoryPublicKey.AsSpan(),
                                                  KeyBlobFormat.RawPublicKey);

            // #6) verify accessory's signature
            var accessorySignature = ConstainsInResponse<PairingException>("Step5", subTlv, TlvType.Signature);
            var accessoryInfo = accessorySessionPublicKeyBytes.Concat(accessoryId)
                                                                .Concat(iosKeyPublic).ToArray();

            if (!SignatureAlgorithm.Ed25519.Verify(accessoryPublicKey, accessoryInfo, accessorySignature))
            {
                Log.Error("{step} error with invalid signature for {Name}", "Step5", connection.DisplayName);
                throw new VerifyPairingException($"Invalid signature error for {connection.DisplayName}");
            }

            // #7) create iOSDeviceInfo
            var iOSPairingIdAsBytes = pairingInfo.GetControllerPairingIdAsBytes();
            var iosDeviceInfo = iosKeyPublic.Concat(iOSPairingIdAsBytes)
                                             .Concat(accessorySessionPublicKeyBytes).ToArray();

            // #8) sign iOSDeviceInfo with long term secret key
            using var iosDevicePrivateKey = Key.Import(ed25519,
                                               pairingInfo.ControllerDevicePrivateKey.AsSpan(),
                                               KeyBlobFormat.RawPrivateKey);

            var ios_device_signature = ed25519.Sign(iosDevicePrivateKey, iosDeviceInfo);

            // #9) construct sub tlv
            var senderSubTlvBytes = Tlv8.Encode(
                new TlvValue[] {
                    new TlvValue( TlvType.Identifier, iOSPairingIdAsBytes),
                    new TlvValue( TlvType.Signature, ios_device_signature),
                });

            // #10) encrypt and sign
            var encryptedDataWithAuthTag = ChaChaEncrypt(sessionKey, "PV-Msg03", senderSubTlvBytes);

            // #11) create tlv
            var requestTlv = new TlvValue[] {
                    new TlvValue( TlvType.State, Tlv8.M3),
                    new TlvValue( TlvType.EncryptedData, encryptedDataWithAuthTag)
                };

            var step11ResponseDict = await PostTlv(requestTlv, PairVerifyTarget, string.Empty, cancellationToken)
                                                  .ConfigureAwait(false);

            HandlePairingResponse<VerifyPairingException>("Step11", step11ResponseDict, Tlv8.M4);

            var controllerToAccessoryKey = HkdfDerive(sharedSecret, "Control-Salt", "Control-Write-Encryption-Key");
            var accessoryToControllerKey = HkdfDerive(sharedSecret, "Control-Salt", "Control-Read-Encryption-Key");

            return new SessionKeys(controllerToAccessoryKey.ToImmutableArray(),
                                   accessoryToControllerKey.ToImmutableArray());
        }

        public async Task RemovePairing(PairingDeviceInfo pairingInfo,
                                        CancellationToken cancellationToken)
        {
            var tlvValues = new TlvValue[] {
                new TlvValue( TlvType.State, Tlv8.M1),
                new TlvValue( TlvType.Method, Tlv8.RemovePairing),
                new TlvValue( TlvType.Identifier, pairingInfo.GetControllerPairingIdAsBytes()),
            };

            var responseDict = await PostTlv(tlvValues, PairingTarget, string.Empty, cancellationToken)
                                          .ConfigureAwait(false);

            HandlePairingResponse<PairingException>("Step", responseDict, Tlv8.M2);
        }

        public async Task<PairingDeviceInfo> StartNewPairing(string pin,
                                                             CancellationToken cancellationToken)
        {
            const string Username = "Pair-Setup";

            bool authRequired = (connection.DeviceFeature & DeviceFeature.SupportsAppleAuthenticationCoprocessor)
                                    == DeviceFeature.SupportsAppleAuthenticationCoprocessor;

            //Step1
            Log.Debug("#1 ios -> accessory: send SRP start request for {EndPoint}", connection.Address);
            var step1ResponseDict = await NewPairingStep1(authRequired, cancellationToken).ConfigureAwait(false);
            var salt = ConstainsInResponse<PairingException>("Step1", step1ResponseDict, TlvType.Salt);
            var serverPublicKey = ConstainsInResponse<PairingException>("Step1", step1ResponseDict, TlvType.PublicKey);

            //Step3
            Log.Debug("#2 ios -> accessory: send SRP start request for {EndPoint}", connection.Address);
            var srpClient2 = new SrpClient(Username, pin, salt, serverPublicKey);

            var step2ResponseDict = await NewPairingStep3(srpClient2, cancellationToken).ConfigureAwait(false);
            var serverProof = ConstainsInResponse<PairingException>("Step3", step2ResponseDict, TlvType.Proof);

            bool verified = srpClient2.VerifyServersProof(SrpInteger.FromByteArray(serverProof));
            if (!verified)
            {
                Log.Error("{step} pairing server proof failed for {Name}", "Step3", connection.DisplayName);
                throw new PairingException($"Server Proof failed for for {connection.DisplayName}");
            }

            //Last step
            Log.Debug("#3 ios -> accessory: send SRP start request for {EndPoint}", connection.Address);
            var result = await NewPairingStep5(srpClient2, cancellationToken).ConfigureAwait(false);

            Log.Information("Pairing complete for {Name}", connection.DisplayName);
            return result;
        }

        private static byte[] HkdfDerive(SharedSecret sharedSecret, string salt, string info)
        {
            return KeyDerivationAlgorithm.HkdfSha512.DeriveBytes(
                sharedSecret,
                Encoding.UTF8.GetBytes(salt),
                Encoding.UTF8.GetBytes(info),
                32);
        }

        private static byte[] HkdfDerive(ReadOnlySpan<byte> sessionKey, string salt, string info)
        {
            using var sharedSecret = SharedSecret.Import(sessionKey);
            return HkdfDerive(sharedSecret, salt, info);
        }

        private bool ChaChaDecrypt(byte[] sessionKey, string nonceCounter, ReadOnlySpan<byte> ciphertext, out byte[]? plaintext)
        {
            using var key = Key.Import(chaCha20Poly1305, sessionKey, KeyBlobFormat.RawSymmetricKey);
            return ChaChaDecrypt(key, nonceCounter, ciphertext, out plaintext);
        }

        private bool ChaChaDecrypt(Key key, string nonceCounter, ReadOnlySpan<byte> ciphertext, out byte[]? plaintext)
        {
            var nonce = new Nonce(fixedFieldNouce, Encoding.ASCII.GetBytes(nonceCounter));
            return chaCha20Poly1305.Decrypt(key, nonce, Array.Empty<byte>(), ciphertext, out plaintext);
        }

        private byte[] ChaChaEncrypt(byte[] sessionKey, string nonceCounter, ReadOnlySpan<byte> plaintext)
        {
            using var key = Key.Import(chaCha20Poly1305, sessionKey, KeyBlobFormat.RawSymmetricKey);
            return ChaChaEncrypt(key, nonceCounter, plaintext);
        }

        private byte[] ChaChaEncrypt(Key key, string nonceCounter, ReadOnlySpan<byte> plaintext)
        {
            var nonce = new Nonce(fixedFieldNouce, Encoding.ASCII.GetBytes(nonceCounter));
            return chaCha20Poly1305.Encrypt(key, nonce, Array.Empty<byte>(), plaintext);
        }

        private byte[] ConstainsInResponse<T>(string step,
                                              ILookup<TlvType, TlvValue> response,
                                              TlvType type) where T : Exception, new()
        {
            if (!response.Contains(type))
            {
                Log.Error("{step} pairing did not return {type} for {Name}", step, type, connection.DisplayName);
                string message = $"{step} of pairing did not return {type} for {connection.DisplayName}";
                throw (T)Activator.CreateInstance(typeof(T), message);
            }
            else
            {
                var tlvValue = response[type].Single();
                return tlvValue.Value.ToArray();
            }
        }

        private void HandlePairingResponse<T>(string step,
                                              ILookup<TlvType, TlvValue> response,
                                              IEnumerable<byte> expectedStateValue) where T : Exception, new()
        {
            if (response.Contains(TlvType.State))
            {
                var stateValue = response[TlvType.State].First();

                if (!Enumerable.SequenceEqual(stateValue.Value, expectedStateValue))
                {
                    Log.Error("{step} pairing did not return correct State for {Name}", step, connection.DisplayName);
                    string message = $"{step} of pairing did not return expected state for {connection.DisplayName}";
                    throw (T)Activator.CreateInstance(typeof(T), message);
                }
            }

            if (response.Contains(TlvType.Error))
            {
                var errorValue = response[TlvType.Error].First();

                string error = (errorValue.Value.Length == 1) ?
                   ((TlvErrorCode)errorValue.Value[0]).ToString() :
                   BitConverter.ToString(errorValue.Value.ToArray());

                Log.Error("{step} pairing failed with Error:{error} for {Name}", step, error, connection.DisplayName);
                string message = $"Pairing for {connection.DisplayName} failed with {error} error";
                throw (T)Activator.CreateInstance(typeof(T), message);
            }
        }

        private async Task<ILookup<TlvType, TlvValue>>
            NewPairingStep1(bool authRequired, CancellationToken cancellationToken)
        {
            var tlvDataStep1 = new TlvValue[] {
                new TlvValue( TlvType.State, Tlv8.M1),
                new TlvValue( TlvType.Method, authRequired ? Tlv8.PairSetupWithAuth : Tlv8.PairSetup),
            };

            var step1ResponseDict = await PostTlv(tlvDataStep1, PairingSetupTarget, string.Empty, cancellationToken)
                                .ConfigureAwait(false);

            HandlePairingResponse<PairingException>("Step1", step1ResponseDict, Tlv8.M2);
            return step1ResponseDict;
        }

        private async Task<ILookup<TlvType, TlvValue>>
                                    NewPairingStep3(SrpClient srpClient2, CancellationToken cancellationToken)
        {
            var clientPublicKey = srpClient2.GetPublicKey();
            var proof = srpClient2.GetProof();

            var tlvDataStep2 = new TlvValue[] {
                new TlvValue( TlvType.State, Tlv8.M3),
                new TlvValue( TlvType.PublicKey, clientPublicKey.ToByteArray()),
                new TlvValue( TlvType.Proof, proof.ToByteArray()),
            };

            var step2ResponseDict = await PostTlv(tlvDataStep2, PairingSetupTarget, string.Empty, cancellationToken).ConfigureAwait(false);
            HandlePairingResponse<PairingException>("Step3", step2ResponseDict, Tlv8.M4);
            return step2ResponseDict;
        }

        private async Task<PairingDeviceInfo> NewPairingStep5(SrpClient srpClient2,
                                                              CancellationToken cancellationToken)
        {
            const string stepName = "Step5";
            Guid pairingId = Guid.NewGuid();
            var clientSessionKey = srpClient2.GetSessionKey();

            var algorithmEd25519 = SignatureAlgorithm.Ed25519;
            using var iosDeviceLtsk = Key.Create(algorithmEd25519,
                                             new KeyCreationParameters()
                                             {
                                                 ExportPolicy = KeyExportPolicies.AllowPlaintextExport
                                             });
            var iosDevicePublicBytes = iosDeviceLtsk.Export(KeyBlobFormat.RawPublicKey);

            var iosDeviceX = HkdfDerive(clientSessionKey.ToByteArray(),
                                           "Pair-Setup-Controller-Sign-Salt",
                                           "Pair-Setup-Controller-Sign-Info");

            var sessionKey = HkdfDerive(clientSessionKey.ToByteArray(),
                                            "Pair-Setup-Encrypt-Salt",
                                            "Pair-Setup-Encrypt-Info");

            var pairingIdBytes = PairingDeviceInfo.EncodeGuid(pairingId);
            var iosDeviceInfo = iosDeviceX.Concat(pairingIdBytes)
                                              .Concat(iosDevicePublicBytes).ToArray();

            var iosDeviceSignature = algorithmEd25519.Sign(iosDeviceLtsk, iosDeviceInfo);

            var subTlv = new TlvValue[] {
                new TlvValue( TlvType.Identifier, pairingIdBytes),
                new TlvValue( TlvType.PublicKey, iosDevicePublicBytes),
                new TlvValue( TlvType.Signature, iosDeviceSignature),
            };

            var subTlvBytes = Tlv8.Encode(subTlv);

            using var chachaKey = Key.Import(chaCha20Poly1305, sessionKey, KeyBlobFormat.RawSymmetricKey);
            var encrypted_data_with_auth_tag = ChaChaEncrypt(chachaKey, "PS-Msg05", subTlvBytes);

            var tlvDataStep3 = new TlvValue[] {
                new TlvValue( TlvType.State, Tlv8.M5),
                new TlvValue( TlvType.EncryptedData, encrypted_data_with_auth_tag),
             };

            var step3ResponseDict = await PostTlv(tlvDataStep3, PairingSetupTarget, string.Empty, cancellationToken)
                                         .ConfigureAwait(false);

            HandlePairingResponse<PairingException>(stepName, step3ResponseDict, Tlv8.M6);

            var encryptedData = ConstainsInResponse<PairingException>(stepName, step3ResponseDict, TlvType.EncryptedData);

            if (!ChaChaDecrypt(chachaKey, "PS-Msg06", encryptedData, out var plainData) || plainData == null)
            {
                Log.Error("{step} data cannot be decrypted for {Name}", stepName, connection.DisplayName);
                throw new PairingException($"Data cannot be decrypted for {connection.DisplayName}");
            }

            var descryptedTlvResponse = Tlv8.Decode(plainData).ToLookup(x => x.Type);

            var accessorySignature = ConstainsInResponse<PairingException>(stepName, descryptedTlvResponse, TlvType.Signature);
            var accessoryIdentifier = ConstainsInResponse<PairingException>(stepName, descryptedTlvResponse, TlvType.Identifier);
            var accessoryPublicKey = ConstainsInResponse<PairingException>(stepName, descryptedTlvResponse, TlvType.PublicKey);

            var accessoryX = HkdfDerive(clientSessionKey.ToByteArray(),
                                        "Pair-Setup-Accessory-Sign-Salt",
                                        "Pair-Setup-Accessory-Sign-Info");

            var accessoryInfo = accessoryX.Concat(accessoryIdentifier)
                                          .Concat(accessoryPublicKey).ToArray();

            var accessoryNSecPublicKey = PublicKey.Import(algorithmEd25519, accessoryPublicKey, KeyBlobFormat.RawPublicKey);

            if (!algorithmEd25519.Verify(accessoryNSecPublicKey, accessoryInfo, accessorySignature))
            {
                Log.Error("{step} signature verification failed {Name}", stepName, connection.DisplayName);
                throw new PairingException($"signature verification failed for {connection.DisplayName}");
            }

            var iosDeviceLtskPrivateBytes = iosDeviceLtsk.Export(KeyBlobFormat.RawPrivateKey);

            return new PairingDeviceInfo(connection.DeviceInformation,
                                         accessoryIdentifier.ToImmutableArray(),
                                         accessoryPublicKey.ToImmutableArray(),
                                         pairingId,
                                         iosDeviceLtskPrivateBytes.ToImmutableArray(),
                                         iosDevicePublicBytes.ToImmutableArray(),
                                         true,
                                         TimeSpan.FromSeconds(60));
        }

        private async Task<ILookup<TlvType, TlvValue>> PostTlv(IEnumerable<TlvValue> tlvData,
                                                               string target,
                                                               string query,
                                                               CancellationToken cancellationToken)
        {
            var stepResponse = await connection.PostTlv(tlvData, target, query, cancellationToken: cancellationToken)
                                .ConfigureAwait(false);

            var step2ResponseDict = stepResponse.ToLookup(x => x.Type);
            return step2ResponseDict;
        }

        private const string PairingSetupTarget = "/pair-setup";
        private const string PairingTarget = "/pairings";
        private const string PairVerifyTarget = "/pair-verify";
        private static readonly byte[] fixedFieldNouce = new byte[] { 0, 0, 0, 0 };
        private readonly ChaCha20Poly1305 chaCha20Poly1305 = AeadAlgorithm.ChaCha20Poly1305;
        private readonly Connection connection;
    }
}