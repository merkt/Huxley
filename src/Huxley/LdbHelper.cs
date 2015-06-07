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
using System.Linq;
using Huxley.ldbServiceReference;

namespace Huxley
{
    public static class LdbHelper
    {
        public static AccessToken MakeAccessToken(Guid accessToken, HuxleySettings huxleySettings)
        {
            // If ClientAccessToken is an empty GUID then no token is required in the Huxley URL.
            // If ClientAccessToken matches the token in the URL then the DarwinAccessToken will be used instead in the SOAP call.
            // Otherwise the URL token is passed straight through
            if (huxleySettings.ClientAccessToken == accessToken)
            {
                accessToken = huxleySettings.DarwinAccessToken;
            }
            return new AccessToken {TokenValue = accessToken.ToString()};
        }

        public static string MakeCrsCode(string query, IEnumerable<CrsRecord> crsRecords)
        {
            var crsRecordArray = crsRecords as CrsRecord[] ?? crsRecords.ToArray();
            
            // Process CRS codes if query is present
            if (!string.IsNullOrWhiteSpace(query) &&
                // If query is not in the list of CRS codes
                !crsRecordArray.Any(c =>
                    c.CrsCode.Equals(query, StringComparison.InvariantCultureIgnoreCase)))
            {
                // And query matches a single station name
                var results = crsRecordArray.Where(c =>
                    c.StationName.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                if (results.Count == 1)
                {
                    // Return the only possible CRS code
                    return results[0].CrsCode;
                }
                // If more than one match then return one if it matches exactly
                if (results.Count > 1)
                {
                    var bestMatch = results.FirstOrDefault(r =>
                        r.StationName.Equals(query, StringComparison.InvariantCultureIgnoreCase));
                    if (null != bestMatch)
                    {
                        return bestMatch.CrsCode;
                    }
                }
            }
            // Otherwise return the query as is
            return query;
        }
    }
}