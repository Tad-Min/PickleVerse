using System;
using System.Collections.Generic;

namespace PickleVerse.Multiplayer
{
    // Stable database identities, shared by Unity and the API. These accounts cannot sign in.
    public static class RankedBotAccounts
    {
        public static readonly IReadOnlyList<string> Ids = Array.AsReadOnly(new[] {
            "3ec8bb2dbe02444c9e5de28f61a91a56", "144b9001fa774116b7e1e73e7b28ac63",
            "a1851f31a754464ba32d498d5d6fb7ef", "8517f87744764199b8e08760de323acb",
            "87c71481088d43569b01038316fc2863", "30b4f6ca65f84a538521fc38c5224560",
            "64a7c55b33a6434bb191e25067af69e1", "9034cf523f5347e2a043265d3eed515b",
            "09da8e832ab64fe6a55a7d6dfcab469a", "ace8686c10d54d63a8d1cd10a1980afa"
        });
        public static bool Contains(string id)
        {
            if (!Guid.TryParse(id, out var parsed))
                return false;
            var key = parsed.ToString("N");
            foreach (var value in Ids)
                if (value == key)
                    return true;
            return false;
        }
    }
}
