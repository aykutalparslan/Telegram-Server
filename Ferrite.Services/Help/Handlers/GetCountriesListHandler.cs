// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.help;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetCountriesListHandler
{
    [TLFunction(Constructors.baseLayer_GetCountriesList)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        using var countryCode = CountryCode.Builder()
            .CountryCodeProperty("90"u8)
            .Patterns(BuildPatterns())
            .Build();
        Vector countryCodes = new();
        countryCodes.AppendTLObject(countryCode.ToReadOnlySpan());

        using var country = Country.Builder()
            .Iso2("TR"u8)
            .DefaultName("Turkey"u8)
            .Name("Turkey"u8)
            .CountryCodes(countryCodes)
            .Build();
        Vector countries = new();
        countries.AppendTLObject(country.ToReadOnlySpan());

        var result = CountriesList.Builder()
            .Countries(countries)
            .Hash(0)
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }

    private static VectorOfString BuildPatterns()
    {
        VectorOfString patterns = new();
        patterns.AppendTLBytes("XXX XXX XX XX"u8);
        return patterns;
    }
}
