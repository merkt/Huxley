using System;
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

        Establish context = () =>
        {
            _ldbClient = new LdbClient(new LDBServiceSoapClient());
            _delaysController = new DelaysController(_ldbClient);
            _stationBoardRequest = new StationBoardRequest { AccessToken = new Guid(), Board = Board.All};
        };

        Because of = () => _response = _delaysController.Get(_stationBoardRequest).Await().AsTask.Result;

        It should_return_a_response = () => _response.ShouldNotBeNull();
    }
}