using Friflo.Engine.ECS;
using UnityEngine;

public class FrifloEngineSystem : MonoBehaviour
{
    private ArchetypeQuery<Position> query;
    // Start is called before the first frame update
    void Start()
    {
        int entityCount = 1_000;
        var store = new EntityStore();
        for (int n = 0; n < entityCount; n++) {
            store.Batch()
                .Add(new Position(n, 0, 0))
                .CreateEntity();    
        }
        query = store.Query<Position>();
    }

    // Update is called once per frame
    void Update()
    {
        foreach (var (positions, entities) in query.Chunks)
        {
            foreach (ref var position in positions.Span) {
                position.y++;
            }
        }
    }
}
