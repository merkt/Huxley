/*
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

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Huxley.Models;
using Huxley.DarwinService;

namespace Huxley.Controllers
{
    public class StationController : ApiController
    {
        private readonly ILdbClient _client;
        private readonly HuxleySettings _huxleySettings;
        private readonly IEnumerable<CrsRecord> _crsRecords;

        public StationController(ILdbClient client, HuxleySettings settings, IEnumerable<CrsRecord> crsRecords)
        {
            _client = client;
            _huxleySettings = settings;
            _crsRecords = crsRecords;
        }

        // GET /{board}/CRS?accessToken=[your token]
        public async Task<StationBoard> Get([FromUri] StationBoardRequest request)
        {
            // Process CRS codes
            request.Crs = LdbHelper.GetCrsCode(request.Crs, _crsRecords);
            request.FilterCrs = LdbHelper.GetCrsCode(request.FilterCrs, _crsRecords);

            var token = LdbHelper.GetDarwinAccessToken(request.AccessToken, _huxleySettings);

            switch (request.Board)
            {
                case Board.Departures:
                    var departures =
                        await
                            _client.GetDepartureBoardAsync(token, request.NumRows, request.Crs, request.FilterCrs,
                                request.FilterType, 0, 0);
                    return departures.GetStationBoardResult;
                case Board.Arrivals:
                    var arrivals =
                        await
                            _client.GetArrivalBoardAsync(token, request.NumRows, request.Crs, request.FilterCrs,
                                request.FilterType, 0, 0);
                    return arrivals.GetStationBoardResult;
                default:
                    var board =
                        await
                            _client.GetArrivalDepartureBoardAsync(token, request.NumRows, request.Crs, request.FilterCrs,
                                request.FilterType, 0, 0);
                    return board.GetStationBoardResult;
            }
        }
    }
}