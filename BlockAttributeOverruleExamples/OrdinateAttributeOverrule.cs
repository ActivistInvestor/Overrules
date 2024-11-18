using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

/// OrdinateAttributeOverrule.cs
/// 
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under terms of the MIT license.

namespace AcMgdLib.DatabaseServices.Examples
{
   /// <summary>
   /// An example demonstrating the use of the 
   /// BlockAttributeOverrule class.
   /// 
   /// This example will autonomously update specific
   /// attributes of specific blocks to display the X 
   /// and Y ordinates of the owning BlockReference's 
   /// insertion point.
   /// 
   /// The example targets a block named "TESTBLOCK", 
   /// having 2 attributes with the tags "X" and "Y",
   /// which is included in the accompanying sample
   /// DWG file.
   /// 
   /// The overrule functionality is enabled by issuing
   /// the ORDINATEATTRIB command.
   /// 
   /// Once the overrule is enabled, and there are one or more
   /// insertions of the TESTBLOCK in the current document, try
   /// using any of the following and note that the attributes
   /// will always display the X and Y ordinates of the owning
   /// block's insertion point:
   /// 
   ///     MOVE command
   ///     STRETCH command
   ///     GRIP_MOVE/STRETCH commands
   ///     COPY command
   ///     CUT/COPYCLIP and PASTECLIP commands
   ///     PROPERTIES Palette (any property change).
   ///     
   /// </summary>

   public class OrdinateAttributeOverrule : BlockAttributeOverrule
   {
      static OrdinateAttributeOverrule instance = null;

      /// <summary>
      /// Constructor:
      /// 
      /// The block name parameter is a wildcard that can be
      /// used to specify multiple block names using standard
      /// AutoCAD-style wildcard matching. The value supplied
      /// to the base constructor below will act on any block
      /// whose name contains the string 'TESTBLOCK'.
      /// </summary>

      public OrdinateAttributeOverrule()
         : base(true, "*TESTBLOCK*", "X", "Y")
      {
         /// Setting this property to true will cause the
         /// overrule to act on newly-inserted blocks, but
         /// attributes of those blocks must be Preset to
         /// avoid prompting for them. 
         
         InsertEnabled = true; 
      }

      /// <summary>
      /// The reusability baked into the base types allows the 
      /// operation to be easily implemented with minimal code:
      /// </summary>

      protected override void Update(AttributeReference att, BlockReference owner)
      {
         Point3d pos = owner.Position;
         switch(att.Tag)
         {
            case "X":
               att.TextString = $"X = {pos.X:0.00}";
               break;
            case "Y":
               att.TextString = $"Y = {pos.Y:0.00}";
               break;
            default:
               break;
         }
      }

      /// <summary>
      /// A command to enable/disable the overrule:
      /// </summary>
      
      [CommandMethod("ORDINATEATTRIB")]
      public static void StartStop()
      {
         if(instance == null)
            instance = new OrdinateAttributeOverrule();
         else
         {
            instance.Dispose();
            instance = null;
         }
         var what = instance != null ? "enabled" : "disabled";
         AppUtils.Write($"\nOrdinate Attribute Overrule {what}");
      }
   }


}
