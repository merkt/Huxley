using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Machine.Specifications;

namespace Huxley.Tests
{
    [Subject(typeof(HuxleyApi))]
    public class When_CRS_codes_retrieved_from_NRE
    {
        private static ISet<CrsRecord> _nreCodes;

        Because of = () => _nreCodes = HuxleyApi.GetCrsCodesFromNreAsync().Await().AsTask.Result;

        It should_not_return_null = () => _nreCodes.ShouldNotBeNull();
        It should_return_a_non_empty_list = () => _nreCodes.ShouldNotBeEmpty();
    }

    [Subject(typeof(HuxleyApi))]
    public class When_CRS_codes_retrieved_from_NaPTAN
    {
        private static ISet<CrsRecord> _naptanCodes;

        Because of = () => _naptanCodes = HuxleyApi.GetCrsCodesFromNaptanAsync().Await().AsTask.Result;

        It should_not_return_null = () => _naptanCodes.ShouldNotBeNull();
        It should_return_a_non_empty_list = () => _naptanCodes.ShouldNotBeEmpty();
    }

    [Subject(typeof(HuxleyApi))]
    public class When_CRS_codes_retrieved
    {
        private static IEnumerable<CrsRecord> _crsCodes;
        private static string _binPath;

        Establish context = () => _binPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);

        Because of = () => _crsCodes = HuxleyApi.GetCrsCodes(Path.Combine(_binPath, "RailReferences.csv")).Await().AsTask.Result;

        It should_not_return_null = () => _crsCodes.ShouldNotBeNull();
        It should_return_a_non_empty_list = () => _crsCodes.ShouldNotBeEmpty();
    }
}
