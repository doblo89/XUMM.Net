﻿using System.Threading.Tasks;
using XUMM.Net.Clients.Interfaces;
using XUMM.Net.Enums;
using XUMM.Net.Extensions;
using XUMM.Net.Helpers;
using XUMM.Net.Models.Misc;

namespace XUMM.Net.Clients
{
    public class XummClientMisc : IXummClientMisc
    {
        private readonly XummClient _xummClient;

        /// <inheritdoc />
        public IXummClientMiscAppStorage AppStorage { get; }

        internal XummClientMisc(XummClient xummClient)
        {
            AppStorage = new XummClientMiscAppStorage(xummClient);
            _xummClient = xummClient;
        }

        /// <inheritdoc />
        public async Task<XummPong> PingAsync()
        {
            return await _xummClient.GetAsync<XummPong>("platform/ping");
        }

        /// <inheritdoc />
        public async Task<XummCuratedAssets> GetCuratedAssetsAsync()
        {
            return await _xummClient.GetAsync<XummCuratedAssets>("platform/curated-assets");
        }

        /// <inheritdoc />
        public async Task<XummTransaction> GetTransactionAsync(string txHash)
        {
            return await _xummClient.GetAsync<XummTransaction>($"platform/xrpl-tx/{txHash}");
        }

        /// <inheritdoc />
        public async Task<XummKycStatus> GetKycStatusAsync(string userTokenOrAccount)
        {
            if (userTokenOrAccount.IsAccountAddress())
            {
                var kycInfo = await _xummClient.GetAsync<XummKycInfo>($"platform/kyc-status/{userTokenOrAccount}", isPublicEndpoint: true);
                return kycInfo.KycApproved ? XummKycStatus.Successful : XummKycStatus.None;
            }
            else
            {
                var request = new XummKycStatusRequest { UserToken = userTokenOrAccount };
                var kycInfo = await _xummClient.PostAsync<XummKycStatusInfo>("platform/kyc-status", request);
                return EnumHelper.GetValueFromName<XummKycStatus>(kycInfo.KycStatus);
            }
        }

        /// <inheritdoc />
        public async Task<XummRates> GetRatesAsync(string currencyCode)
        {
            return await _xummClient.GetAsync<XummRates>($"platform/rates/{currencyCode.Trim().ToUpperInvariant()}");
        }
    }
}
