using System;
using Formo;
using Huxley.Controllers;
using Huxley.ldbServiceReference;
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

        Establish context = () =>
        {
            dynamic config = new Configuration();
            _settings = config.Bind<HuxleySettings>();

            _ldbClient = new LdbClient(new LDBServiceSoapClient());
            _delaysController = new DelaysController(_ldbClient, _settings);
            // delays/clapham%20junction/from/london/20?accessToken=
            _stationBoardRequest = new StationBoardRequest
            {
                AccessToken = new Guid(),
                Crs = "clapham junction",
                FilterType = FilterType.@from,
                FilterCrs = "london",
                NumRows = 25
            };
        };

        Because of = () => _response = _delaysController.Get(_stationBoardRequest).Await().AsTask.Result;

        It should_return_a_response = () => _response.ShouldNotBeNull();
    }
}