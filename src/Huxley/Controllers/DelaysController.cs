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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Huxley.Models;
using Huxley.ldbServiceReference;

namespace Huxley.Controllers
{
    public class DelaysController : ApiController
    {
        private readonly ILdbClient _client;
        private readonly HuxleySettings _huxleySettings;
        private readonly IEnumerable<CrsRecord> _crsRecords;

        public DelaysController(ILdbClient client, HuxleySettings settings, IEnumerable<CrsRecord> crsRecords)
        {
            _client = client;
            _huxleySettings = settings;
            _crsRecords = crsRecords;
        }

        // GET /delays/{crs}/{filtertype}/{filtercrs}/{numrows}/{stds}?accessToken=[your token]
        public async Task<DelaysResponse> Get([FromUri] StationBoardRequest request)
        {
            if (request.AccessToken == null)
                throw new HttpResponseException(HttpStatusCode.BadRequest);

            // Process CRS codes
            request.Crs = LdbHelper.MakeCrsCode(request.Crs, _crsRecords);
            request.FilterCrs = LdbHelper.MakeCrsCode(request.FilterCrs, _crsRecords);

            // Parse the list of comma separated STDs if provided (e.g. /btn/to/lon/50/0729,0744,0748)
            List<string> stds;
            if (!ParseStds(request.Std, out stds)) return new DelaysResponse();

            var totalDelayMinutes = 0;
            var delayedTrains = new List<ServiceItem>();

            var token = LdbHelper.MakeAccessToken(request.AccessToken, _huxleySettings);

            var filterCrs = request.FilterCrs;

            if (filterCrs == null)
                throw new HttpResponseException(HttpStatusCode.BadRequest);

            if (request.FilterCrs.Equals("LON", StringComparison.InvariantCultureIgnoreCase) ||
                request.FilterCrs.Equals("London", StringComparison.InvariantCultureIgnoreCase))
            {
                filterCrs = null;
            }

            var board =
                await
                    _client.GetDepartureBoardAsync(token, request.NumRows, request.Crs, filterCrs, request.FilterType, 0,
                        0);

            var response = board.GetStationBoardResult;
            var filterLocationName = response.filterLocationName;

            var trainServices = response.trainServices ?? new ServiceItem[0];
            var railReplacement = null != response.busServices && !trainServices.Any() && response.busServices.Any();
            var messagesPresent = null != response.nrccMessages && response.nrccMessages.Any();

            if (null == filterCrs)
            {
                // This only finds trains terminating at London terminals. BFR/STP etc. won't be picked up if called at en-route.
                // Could query for every terminal or get service for every train and check calling points. Very chatty either way.
                switch (request.FilterType)
                {
                    case FilterType.to:
                        trainServices =
                            trainServices.Where(
                                ts =>
                                    ts.destination.Any(
                                        d => CrsRecord.LondonTerminals.Any(lt => lt.CrsCode == d.crs.ToUpperInvariant())))
                                .ToArray();
                        break;
                    case FilterType.from:
                        trainServices =
                            trainServices.Where(
                                ts =>
                                    ts.origin.Any(
                                        o => CrsRecord.LondonTerminals.Any(lt => lt.CrsCode == o.crs.ToUpperInvariant())))
                                .ToArray();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                filterCrs = "LON";
                filterLocationName = "London";
            }

            // If STDs are provided then select only the train(s) matching them
            if (stds.Count > 0)
            {
                trainServices = trainServices.Where(ts => stds.Contains(ts.std.Replace(":", ""))).ToArray();
            }

            // Parse the response from the web service.
            foreach (
                var si in
                    trainServices.Where(si => !si.etd.Equals("On time", StringComparison.InvariantCultureIgnoreCase)))
            {
                if (si.etd.Equals("Delayed", StringComparison.InvariantCultureIgnoreCase) ||
                    si.etd.Equals("Cancelled", StringComparison.InvariantCultureIgnoreCase))
                {
                    delayedTrains.Add(si);
                }
                else
                {
                    DateTime etd;
                    // Could be "Starts Here", "No Report" or contain a * (report overdue)
                    if (DateTime.TryParse(si.etd.Replace("*", ""), out etd))
                    {
                        DateTime std;
                        if (DateTime.TryParse(si.std, out std))
                        {
                            // TODO: fix this calculation
                            var late = etd.Subtract(std);
                            totalDelayMinutes += (int) late.TotalMinutes;
                            if (late.TotalMinutes > _huxleySettings.DelayMinutesThreshold)
                            {
                                delayedTrains.Add(si);
                            }
                        }
                    }
                }
            }

            return new DelaysResponse
            {
                GeneratedAt = response.generatedAt,
                Crs = response.crs,
                LocationName = response.locationName,
                Filtercrs = filterCrs,
                FilterLocationName = filterLocationName,
                Delays = delayedTrains.Count > 0 || railReplacement || messagesPresent,
                TotalTrainsDelayed = delayedTrains.Count,
                TotalDelayMinutes = totalDelayMinutes,
                TotalTrains = trainServices.Length,
                DelayedTrains = delayedTrains,
            };
        }

        static bool ParseStds(string stdCsvString, out List<string> stds)
        {
            stds = new List<string>();
            if (!string.IsNullOrWhiteSpace(stdCsvString))
            {
                var potentialStds = stdCsvString.Split(',');
                var ukNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"));
                var dontRequest = 0;
                foreach (var potentialStd in potentialStds)
                {
                    DateTime requestStd;
                    // Parse the STD in 24-hour format (with no colon)
                    if (
                        !DateTime.TryParseExact(potentialStd, "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None,
                            out requestStd))
                    {
                        continue;
                    }
                    stds.Add(potentialStd);
                    var diff = requestStd.Subtract(ukNow);
                    if (diff.TotalHours > 2 || diff.TotalHours < -1)
                    {
                        dontRequest++;
                    }
                }
                // Don't make a request if all trains are more than 2 hours in the future or more than 1 hour in the past
                if (stds.Count > 0 && stds.Count == dontRequest)
                {
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}