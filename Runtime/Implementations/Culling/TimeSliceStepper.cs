namespace P3k.RoomAndLightingCulling.Implementations.Culling
{
   using System.Linq;

   /// <summary>
   ///    Iterates room indices across frames with optional full sweep.
   /// </summary>
   internal sealed class TimeSliceStepper
   {
      /// <summary>
      ///    Full sweep requested.
      /// </summary>
      private bool _fullSweep;

      /// <summary>
      ///    Current head index.
      /// </summary>
      private int _head;

      /// <summary>
      ///    Clears full sweep flag after one pass.
      /// </summary>
      internal void EndTick()
      {
         _fullSweep = false;
      }

      /// <summary>
      ///    Returns number of iterations to run this tick.
      /// </summary>
      internal int GetIterations(int total, int checksPerFrame)
      {
         if (total <= 0)
         {
            return 0;
         }

         if (_fullSweep)
         {
            return total;
         }

         var c = checksPerFrame < 1 ? 1 : checksPerFrame;
         return c > total ? total : c;
      }

      /// <summary>
      ///    Gets next index and advances. Wraps at total.
      /// </summary>
      internal int Next(int total)
      {
         if (_head >= total)
         {
            _head = 0;
         }

         return _head++;
      }

      /// <summary>
      ///    Requests a full sweep from index 0.
      /// </summary>
      internal void StartFullSweep()
      {
         _fullSweep = true;
         _head = 0;
      }
   }
}
