using System.Collections.Generic;

public enum BiomeType
{
    Forest, Meadow, Temple, Jungle,
    Desert, Ruins, Cave, Volcano,
    Swamp, Beach, Ocean, Abyss,
    Snow, Grave, Circus, Void,Shop
}

public class BiomeNavigator
{
    // 각 바이옴마다 정확히 3개의 다음 목적지 노드를 가집니다
    public readonly Dictionary<BiomeType, List<BiomeType>> BiomeRoutes = new Dictionary<BiomeType, List<BiomeType>>()
    {
        { BiomeType.Forest, new List<BiomeType> { BiomeType.Meadow, BiomeType.Jungle, BiomeType.Cave } },
        { BiomeType.Meadow, new List<BiomeType> { BiomeType.Forest, BiomeType.Desert, BiomeType.Beach } },
        { BiomeType.Temple, new List<BiomeType> { BiomeType.Ruins, BiomeType.Grave, BiomeType.Circus } },
        { BiomeType.Jungle, new List<BiomeType> { BiomeType.Swamp, BiomeType.Ruins, BiomeType.Cave } },
        { BiomeType.Desert, new List<BiomeType> { BiomeType.Volcano, BiomeType.Meadow, BiomeType.Swamp } },
        { BiomeType.Ruins,  new List<BiomeType> { BiomeType.Temple, BiomeType.Cave, BiomeType.Snow } },
        { BiomeType.Cave,   new List<BiomeType> { BiomeType.Volcano, BiomeType.Ruins, BiomeType.Swamp } },
        { BiomeType.Volcano,new List<BiomeType> { BiomeType.Desert, BiomeType.Cave, BiomeType.Snow } },
        { BiomeType.Swamp,  new List<BiomeType> { BiomeType.Jungle, BiomeType.Grave, BiomeType.Abyss } },
        { BiomeType.Beach,  new List<BiomeType> { BiomeType.Meadow, BiomeType.Ocean, BiomeType.Circus } },
        { BiomeType.Ocean,  new List<BiomeType> { BiomeType.Beach, BiomeType.Swamp, BiomeType.Abyss } },
        { BiomeType.Abyss,  new List<BiomeType> { BiomeType.Ocean, BiomeType.Grave, BiomeType.Temple } },
        { BiomeType.Snow,   new List<BiomeType> { BiomeType.Volcano, BiomeType.Forest, BiomeType.Circus } },
        { BiomeType.Grave,  new List<BiomeType> { BiomeType.Temple, BiomeType.Swamp, BiomeType.Abyss } },
        { BiomeType.Circus, new List<BiomeType> { BiomeType.Temple, BiomeType.Beach, BiomeType.Snow } }
    };

    public List<BiomeType> GetNextBiomeOptions(BiomeType currentBiome, int currentStage)
    {
        //100스테이지(바이옴 10개) 클리어 시 무조건 공허(Void) 3개로 고정하여 선택지에 띄움
        if (currentStage >= 10)
        {
            return new List<BiomeType> { BiomeType.Void, BiomeType.Void, BiomeType.Void };
        }

        return BiomeRoutes.ContainsKey(currentBiome) ? BiomeRoutes[currentBiome] : new List<BiomeType>();
    }
}