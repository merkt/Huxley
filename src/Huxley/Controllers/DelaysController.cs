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

            if (request.FilterCrs == null)
                throw new HttpResponseException(HttpStatusCode.BadRequest);

            // Process CRS codes
            request.Crs = LdbHelper.GetCrsCode(request.Crs, _crsRecords);
            request.FilterCrs = LdbHelper.GetCrsCode(request.FilterCrs, _crsRecords);

            List<string> stds;
            // Parse the list of comma separated STDs if provided (e.g. /btn/to/lon/50/0729,0744,0748)
            if (!ParseStds(request.Std, out stds)) 
                return new DelaysResponse();

            var filterCrs = request.FilterCrs;
            StationBoard board;
            ServiceItem[] trainServices;
            string filterLocationName;

            var token = LdbHelper.GetDarwinAccessToken(request.AccessToken, _huxleySettings);

            if (request.FilterCrs.Equals("LON", StringComparison.InvariantCultureIgnoreCase) ||
                request.FilterCrs.Equals("London", StringComparison.InvariantCultureIgnoreCase))
            {
                var response =
                    await
                        _client.GetDepartureBoardAsync(token, request.NumRows, request.Crs, null,
                            request.FilterType, 0,
                            0).ConfigureAwait(false);

                board = response.GetStationBoardResult;

                var londonDepartures = FilterLondonDepartures(board.trainServices, request.FilterType);
                trainServices = londonDepartures as ServiceItem[] ?? londonDepartures.ToArray();

                filterCrs = "LON";
                filterLocationName = "London";
            }
            else
            {
                var response =
                    await
                        _client.GetDepartureBoardAsync(token, request.NumRows, request.Crs, filterCrs,
                            request.FilterType, 0,
                            0).ConfigureAwait(false);
                
                board = response.GetStationBoardResult;
                trainServices = board.trainServices ?? new ServiceItem[0];
                filterLocationName = board.filterLocationName;
            }            

            var railReplacement = board.busServices != null && !trainServices.Any() && board.busServices.Any();
            var messagesPresent = board.nrccMessages != null && board.nrccMessages.Any();

            // If STDs are provided then select only the train(s) matching them
            if (stds.Count > 0)
            {
                trainServices =
                    trainServices.Where(ts => stds.Contains(ts.ScheduledTimeOfDeparture.Replace(":", ""))).ToArray();
            }

            int totalDelayMinutes;
            // TODO: make delay threshold part of request
            var delayedTrains = GetDelayedTrains(trainServices, _huxleySettings.DelayMinutesThreshold,
                out totalDelayMinutes);

            return new DelaysResponse
            {
                GeneratedAt = board.generatedAt,
                Crs = board.crs,
                LocationName = board.locationName,
                Filtercrs = filterCrs,
                FilterLocationName = filterLocationName,
                Delays = delayedTrains.Count > 0 || railReplacement || messagesPresent,
                TotalTrainsDelayed = delayedTrains.Count,
                TotalDelayMinutes = totalDelayMinutes,
                TotalTrains = trainServices.Length,
                DelayedTrains = delayedTrains,
            };
        }

        public static IList<ServiceItem> GetDelayedTrains(IEnumerable<ServiceItem> trainServices, int delayedMinutesThreshold,
            out int totalDelayMinutes)
        {
            totalDelayMinutes = 0;
            var delayedTrains = new List<ServiceItem>();

            // Parse the response from the web service.
            foreach (var service in trainServices)
            {
                if (service.EstimatedTimeOfDeparture.Equals("On time", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (service.EstimatedTimeOfDeparture.Equals("Delayed", StringComparison.InvariantCultureIgnoreCase) ||
                    service.EstimatedTimeOfDeparture.Equals("Cancelled", StringComparison.InvariantCultureIgnoreCase))
                {
                    delayedTrains.Add(service);
                }
                else
                {
                    DateTime estimatedTimeOfDeparture;
                    DateTime scheduledTimeOfDeparture;
                    // Could be "Starts Here", "No Report" or contain a * (report overdue)
                    if (
                        DateTime.TryParse(service.EstimatedTimeOfDeparture.Replace("*", ""),
                            out estimatedTimeOfDeparture) &&
                        DateTime.TryParse(service.ScheduledTimeOfDeparture.Replace("*", ""),
                            out scheduledTimeOfDeparture))
                    {
                        // TODO: fix this calculation
                        var late = estimatedTimeOfDeparture.Subtract(scheduledTimeOfDeparture);
                        totalDelayMinutes += (int) late.TotalMinutes;
                        if (late.TotalMinutes > delayedMinutesThreshold)
                        {
                            delayedTrains.Add(service);
                        }
                    }
                }
            }
            return delayedTrains;
        }

        // TODO: Rename STD to ScheduledTimeOfDeparture
        public static bool ParseStds(string stdCsvString, out List<string> stds)
        {
            stds = new List<string>();
            if (!string.IsNullOrWhiteSpace(stdCsvString))
            {
                var potentialStds = stdCsvString.Split(',');
                var ukNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"));
                var invalidTimesCount = 0;
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
                    // time is invalid if more than 2 hours in the future or more than 1 hour in the past
                    if (diff.TotalHours > 2 || diff.TotalHours < -1)
                    {
                        invalidTimesCount++;
                    }
                }

                if (stds.Count > 0 && stds.Count == invalidTimesCount)
                {
                    return false;
                }
            }
            return true;
        }

        static IEnumerable<ServiceItem> FilterLondonDepartures(IEnumerable<ServiceItem> trainServices,
            FilterType filterType)
        {
            if (trainServices == null)
                return new ServiceItem[0];

            // This only finds trains terminating at London terminals. BFR/STP etc. won't be picked up if called at en-route.
            // Could query for every terminal or get service for every train and check calling points. Very chatty either way.
            switch (filterType)
            {
                case FilterType.to:
                    return
                        trainServices.Where(
                            ts =>
                                ts.destination.Any(
                                    d => CrsRecord.LondonTerminals.Any(lt => lt.CrsCode == d.crs.ToUpperInvariant())))
                            .ToArray();
                case FilterType.from:
                    return
                        trainServices.Where(
                            ts =>
                                ts.origin.Any(
                                    o => CrsRecord.LondonTerminals.Any(lt => lt.CrsCode == o.crs.ToUpperInvariant())))
                            .ToArray();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}