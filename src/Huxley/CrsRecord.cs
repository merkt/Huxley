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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace Huxley
{
    public class CrsRecord : IEquatable<CrsRecord>
    {
        // https://en.wikipedia.org/wiki/London_station_group 
        // Farringdon [ZFD] is not a London Terminal but it probably should be (maybe when Crossrail opens it will be)
        public static readonly IEnumerable<CrsRecord> LondonTerminals = new List<CrsRecord>
        {
            new CrsRecord {CrsCode = "BFR", StationName = "London Blackfriars"},
            new CrsRecord {CrsCode = "CST", StationName = "London Cannon Street"},
            new CrsRecord {CrsCode = "CHX", StationName = "London Charing Cross"},
            new CrsRecord {CrsCode = "CTX", StationName = "City Thameslink"},
            new CrsRecord {CrsCode = "EUS", StationName = "London Euston"},
            new CrsRecord {CrsCode = "FST", StationName = "London Fenchurch Street"},
            new CrsRecord {CrsCode = "KGX", StationName = "London Kings Cross"},
            new CrsRecord {CrsCode = "LST", StationName = "London Liverpool Street"},
            new CrsRecord {CrsCode = "LBG", StationName = "London Bridge"},
            new CrsRecord {CrsCode = "MYB", StationName = "London Marylebone"},
            new CrsRecord {CrsCode = "MOG", StationName = "Moorgate"},
            new CrsRecord {CrsCode = "OLD", StationName = "Old Street"},
            new CrsRecord {CrsCode = "PAD", StationName = "London Paddington"},
            new CrsRecord {CrsCode = "STP", StationName = "London St Pancras International"},
            new CrsRecord {CrsCode = "VXH", StationName = "Vauxhall"},
            new CrsRecord {CrsCode = "VIC", StationName = "London Victoria"},
            new CrsRecord {CrsCode = "WAT", StationName = "London Waterloo"},
            new CrsRecord {CrsCode = "WAE", StationName = "London Waterloo East"}
        };

        public string StationName { get; set; }
        public string CrsCode { get; set; }

        public override int GetHashCode()
        {
            return CrsCode.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;

            if (ReferenceEquals(this, obj)) return true;

            var other = obj as CrsRecord;

            if (other == null) return false;

            return CrsCode.Equals(other.CrsCode);
        }

        public bool Equals(CrsRecord other)
        {
            if (ReferenceEquals(other, null)) return false;

            if (ReferenceEquals(this, other)) return true;

            return CrsCode.Equals(other.CrsCode);
        }

        internal static IEnumerable<CrsRecord> GetCrsCodesSync()
        {
            return Task.Run(() => GetCrsCodesAsync(null)).Result;
        }

        internal static IEnumerable<CrsRecord> GetCrsCodesSync(string embeddedCrsPath)
        {
            return Task.Run(() => GetCrsCodesAsync(embeddedCrsPath)).Result;
        }

        public static async Task<IEnumerable<CrsRecord>> GetCrsCodesAsync(string embeddedCrsPath)
        {
            // Execute both tasks in parallel
            var nreTask = GetCrsCodesFromNreAsync().ConfigureAwait(false);
            var naptanTask = GetCrsCodesFromNaptanAsync().ConfigureAwait(false);

            var nreCodes = await nreTask;
            var naptanCodes = await naptanTask;
            var embeddedCodes = GetCrsCodesFromFilePath(embeddedCrsPath);

            return nreCodes.Union(naptanCodes).Union(embeddedCodes);
        }

        public static async Task<ISet<CrsRecord>> GetCrsCodesFromNaptanAsync()
        {
            // TODO: Move URL to config file
            // NaPTAN - has better data than the NRE list but is missing some entries (updated weekly)
            // Part of this archive https://www.dft.gov.uk/NaPTAN/snapshot/NaPTANcsv.zip along with other modes of transport
            // Contains public sector information licensed under the Open Government Licence v3.0.
            const string naptanRailUrl =
                "https://raw.githubusercontent.com/jpsingleton/Huxley/master/src/Huxley/RailReferences.csv";

            return await GetCrsCodesFromRemoteSourceAsync(naptanRailUrl).ConfigureAwait(false);
        }

        public static async Task<ISet<CrsRecord>> GetCrsCodesFromNreAsync()
        {
            // TODO: Move URL to config file
            // NRE list - incomplete / old (some codes only in NaPTAN work against the Darwin web service)
            const string crsUrl = "http://www.nationalrail.co.uk/static/documents/content/station_codes.csv";

            // Need a custom map as NRE headers are different to NaPTAN
            return await GetCrsCodesFromRemoteSourceAsync<NreCrsRecordMap>(crsUrl).ConfigureAwait(false);
        }

        public static IEnumerable<CrsRecord> GetCrsCodesFromFilePath(string filePath)
        {
            var codes = new HashSet<CrsRecord>();

            if (string.IsNullOrEmpty(filePath))
                return codes;

            try
            {
                // If we can't get the latest version then use the embedded version
                // Might be a little bit out of date but probably good enough
                using (var stream = File.OpenRead(filePath))
                {
                    using (var csvReader = new CsvReader(new StreamReader(stream)))
                    {
                        codes = new HashSet<CrsRecord>(csvReader.GetRecords<CrsRecord>()
                            .Select(c => new CrsRecord { StationName = c.StationName, CrsCode = c.CrsCode }));
                    }
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            return codes;
        }

        private static async Task<ISet<CrsRecord>> GetCrsCodesFromRemoteSourceAsync<T>(string url) where T : CsvClassMap
        {
            return await GetCrsCodesFromRemoteSourceAsync(url, typeof(T)).ConfigureAwait(false);
        }

        private static async Task<ISet<CrsRecord>> GetCrsCodesFromRemoteSourceAsync(string url)
        {
            return await GetCrsCodesFromRemoteSourceAsync(url, null).ConfigureAwait(false);
        }

        private static async Task<ISet<CrsRecord>> GetCrsCodesFromRemoteSourceAsync(string url, Type typeOfClassMap)
        {
            ISet<CrsRecord> codes = new HashSet<CrsRecord>();

            try
            {
                using (var client = new HttpClient())
                {
                    var stream = await client.GetStreamAsync(url).ConfigureAwait(false);
                    using (var csvReader = new CsvReader(new StreamReader(stream)))
                    {
                        if (typeOfClassMap != null)
                            csvReader.Configuration.RegisterClassMap(typeOfClassMap);

                        codes = new HashSet<CrsRecord>(csvReader.GetRecords<CrsRecord>()
                            .Select(c => new CrsRecord
                            {
                                // NaPTAN suffixes most station names with "Rail Station" which we don't want
                                StationName = c.StationName.Replace("Rail Station", string.Empty).Trim(),
                                CrsCode = c.CrsCode
                            }));
                    }
                }
            }
                // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            return codes;
        }
    }
}