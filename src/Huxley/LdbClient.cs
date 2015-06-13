﻿/*
Huxley - a JSON proxy for the UK National Rail Live Departure Board SOAP API
Copyright (C) 2015 James Singleton
 * http://huxley.unop.uk
 * https://github.com/jpsingleton/Huxley

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Threading.Tasks;
using Huxley.DarwinService;

namespace Huxley
{
    public class LdbClient : ILdbClient
    {
        private readonly LDBServiceSoapClient _client;

        public LdbClient(LDBServiceSoapClient client)
        {
            this._client = client;
        }

        public async Task<GetDepartureBoardResponse> GetDepartureBoardAsync(AccessToken accessToken, ushort numRows,
            string crs, string filterCrs,
            FilterType filterType, int timeOffset, int timeWindow)
        {
            return
                await
                    Execute(
                        () =>
                            _client.GetDepartureBoardAsync(accessToken, numRows, crs, filterCrs, filterType, timeOffset,
                                timeWindow));
        }

        public async Task<GetArrivalBoardResponse> GetArrivalBoardAsync(AccessToken accessToken, ushort numRows,
            string crs, string filterCrs,
            FilterType filterType, int timeOffset, int timeWindow)
        {
            return
                await
                    Execute(
                        () =>
                            _client.GetArrivalBoardAsync(accessToken, numRows, crs, filterCrs, filterType, timeOffset,
                                timeWindow));
        }

        public async Task<GetArrivalDepartureBoardResponse> GetArrivalDepartureBoardAsync(AccessToken accessToken,
            ushort numRows, string crs, string filterCrs,
            FilterType filterType, int timeOffset, int timeWindow)
        {
            return
                await
                    Execute(
                        () =>
                            _client.GetArrivalDepartureBoardAsync(accessToken, numRows, crs, filterCrs, filterType,
                                timeOffset, timeWindow));
        }

        public async Task<GetServiceDetailsResponse> GetServiceDetailsAsync(AccessToken accessToken, string serviceId)
        {
            return await Execute(() => _client.GetServiceDetailsAsync(accessToken, serviceId));
        }

        private T Execute<T>(Func<T> func)
        {
            // Avoiding Problems with the Using Statement in WCF clients
            try
            {
                return func();
            }
            catch (Exception)
            {
                _client.Abort();
                throw;
            }
            finally
            {
                _client.Close();
            }
        }
    }
}