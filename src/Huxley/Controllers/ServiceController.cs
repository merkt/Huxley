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
using System.Threading.Tasks;
using System.Web.Http;
using Huxley.Models;
using Huxley.DarwinService;

namespace Huxley.Controllers
{
    public class ServiceController : ApiController
    {
        private readonly ILdbClient _client;
        private readonly HuxleySettings _huxleySettings;
        private readonly IEnumerable<CrsRecord> _crsRecords;

        public ServiceController(ILdbClient client, HuxleySettings settings, IEnumerable<CrsRecord> crsRecords)
        {
            _client = client;
            _huxleySettings = settings;
            _crsRecords = crsRecords;
        }

        // GET /service/ID?accessToken=[your token]
        public async Task<ServiceDetails> Get([FromUri] ServiceRequest request)
        {
            Guid sid;
            if (Guid.TryParse(request.ServiceId, out sid))
            {
                request.ServiceId = Convert.ToBase64String(sid.ToByteArray());
            }
            var token = LdbHelper.GetDarwinAccessToken(request.AccessToken, _huxleySettings);
            var service = await _client.GetServiceDetailsAsync(token, request.ServiceId);
            return service.GetServiceDetailsResult;
        }
    }
}