using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Formo;
using Huxley.Controllers;
using Huxley.DarwinService;
using Huxley.Models;
using Machine.Specifications;

namespace Huxley.Tests.Controllers
{
    [Subject(typeof(DelaysController))]
    public class When_delays_retrieved
    {
        private static ILdbClient _ldbClient;
        private static StationBoardRequest _stationBoardRequest;
        private static DelaysController _delaysController;
        private static DelaysResponse _response;
        private static HuxleySettings _settings;
        private static IEnumerable<CrsRecord> _crsRecords;
        private static string _binPath;

        Establish context = () =>
        {
            dynamic config = new Configuration();
            _settings = config.Bind<HuxleySettings>();
            _binPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

            _crsRecords = CrsRecord.GetCrsCodesFromFilePath(Path.Combine(_binPath, "RailReferences.csv"));

            _ldbClient = new LdbClient(new LDBServiceSoapClient());
            _delaysController = new DelaysController(_ldbClient, _settings, _crsRecords);
            // delays/clapham%20junction/from/london/20?accessToken=
            _stationBoardRequest = new StationBoardRequest
            {
                AccessToken = new Guid(),
                Crs = "clapham junction",
                FilterType = FilterType.@from,
                FilterCrs = "london",
                NumRows = 20
            };
        };

        Because of = () => _response = _delaysController.Get(_stationBoardRequest).Await().AsTask.Result;

        It should_return_a_response = () => _response.ShouldNotBeNull();

        It should_return_a_response_with_a_train_total = () => _response.TotalTrains.ShouldBeGreaterThan(0);
    }
}