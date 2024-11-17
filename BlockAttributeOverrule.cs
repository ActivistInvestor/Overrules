
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;

/// BlockAttributeOverrule.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under terms of the MIT license.

namespace AcMgdLib.DatabaseServices.Extensions
{

   /// <summary>
   /// An example showing the use of the BlockAttributeOverrule 
   /// base type.
   /// 
   /// This example class will autonomously update attributes 
   /// of a specified block to display the X and Y ordinates of 
   /// the owning BlockReference's insertion point.
   /// 
   /// The example targets a block named "TESTBLOCK", 
   /// having 2 attributes with the tags "X" and "Y".
   /// 
   /// The overrule functionality is enabled by issuing
   /// the BLOCKOVERRULE command.
   /// 
   /// Once the overrule is enabled, and there are one or more
   /// insertions of the TESTBLOCK in the current document, try
   /// doing any of the following and note that the attributes
   /// will _always_ display the X and Y of the block insertion
   /// point:
   /// 
   ///     MOVE command
   ///     STRETCH command
   ///     GRIP_MOVE/STRETCH commands
   ///     COPY command
   ///     INSERT command (yes, it updates newly-inserted blocks)
   ///     CUT/COPYCLIP and PASTECLIP commands
   ///     PROPERTIES Palette (any property change).
   ///     
   /// </summary>

   public class MyBlockAttributeOverrule : BlockAttributeOverrule
   {
      static MyBlockAttributeOverrule instance = null;

      /// <summary>
      /// The block name parameter is a wildcard that can be
      /// used to specify multiple block names using standard
      /// AutoCAD-style wildcard matching.
      /// </summary>


      public MyBlockAttributeOverrule()
         : base(true, "TESTBLOCK", "X", "Y")
      {
      }

      /// <summary>
      /// The resuable functionality in the base types
      /// allow the operation to be done with 2 lines
      /// of code.
      /// </summary>

      protected override void UpdateAttribute(AttributeReference att, BlockReference owner)
      {
         var dim = att.Tag == "X" ? owner.Position.X : owner.Position.Y;
         att.TextString = $"{att.Tag}: {dim:0.00}";
      }

      /// <summary>
      /// A command to enable/disable the overrule:
      /// </summary>
      
      [CommandMethod("BLOCKOVERRULE")]
      public static void StartStop()
      {
         if(instance == null)
            instance = new MyBlockAttributeOverrule();
         else
         {
            instance.Dispose();
            instance = null;
         }
      }
   }

   /// The above class is an _Example_. Everything that follows
   /// is reusable code that shouldn't require changes, as all
   /// specifics of/parameters for the operation are encapsulated
   /// in the UpdateAttribute() override in the above class.

   public abstract class BlockAttributeOverrule : ObjectOverrule<BlockReference>
   {
      string pattern;
      static Dictionary<ObjectId, bool> blocks
         = new Dictionary<ObjectId, bool>();
      HashSet<string> tags;

      /// <summary>
      /// Specify a wildcard pattern matching the names of
      /// one or more blocks and one or more strings that
      /// identify the Tags of the attributes to be operated
      /// on. If no tag strings are supplied, all attributes
      /// are operated on.
      /// </summary>

      public BlockAttributeOverrule(bool enabled, string blockName, params string[] tags)
         : base(enabled)
      {
         if(string.IsNullOrWhiteSpace(blockName))
            throw new ArgumentNullException("No block name specified");
         this.pattern = blockName;
         if(tags.Length > 0)
            this.tags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
      }

      /// <summary>
      /// This class is abstract and is designed to be reusable.
      /// That is achieved by deriving a class from this class
      /// and doing little more than overriding this method:
      /// </summary>
      /// <param name="att">The AttributeReference to be operated on</param>
      /// <param name="owner">The owning BlockReference</param>

      protected virtual void UpdateAttribute(AttributeReference att, BlockReference owner)
      {
      }

      protected virtual bool IsMatch(BlockReference br)
      {
         /// Cache the ObjectIds and match result of all
         /// BlockTableRecords, which allows subsequent 
         /// match queries to be reduced to nothing but 
         /// a dictionary lookup. Note that this cache is
         /// Database-agnostic, and will store ObjectIds
         /// from multiple Databases:

         var btrId = br.DynamicBlockTableRecord;
         if(!blocks.TryGetValue(btrId, out bool result))
         {
            string name = GetEffectiveName(br);
            result = blocks[btrId] = name.Matches(pattern);
         }
         return result;
      }

      static string GetEffectiveName(BlockReference br)
      {
         if(!br.IsDynamicBlock)
            return br.Name;
         var btrId = br.DynamicBlockTableRecord;
         using(var btr = (BlockTableRecord)btrId.GetObject(OpenMode.ForRead))
         {
            return btr.Name;
         }
      }

      public sealed override void Close(DBObject obj)
      {
         try
         {
            if(obj.IsWriteEnabled && obj.IsReallyClosing && !obj.IsUndoing
                  && !obj.IsErased && !obj.IsNewObject)
            {
               BlockReference br = obj as BlockReference;
               if(IsMatch(br))
               {
                  using(var tr = new OpenCloseTransaction())
                  {
                     foreach(ObjectId id in br.AttributeCollection)
                     {
                        var attref = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);
                        if(tags == null || tags.Contains(attref.Tag))
                        {
                           attref.UpgradeOpen();
                           UpdateAttribute(attref, br);
                        }
                     }
                     tr.Commit();
                  }
               }
            }
         }
         catch(System.Exception)
         {
            // We will get here if a block is inserted,
            // and ATTDIA == 0. In that case, the attributes
            // are open for write and throw an exception 
            // when attempting to open them.
         }
         base.Close(obj);
      }
   }

   /// <summary>
   /// An implementation of ObjectOverrule that automates 
   /// adding/removing the overrule from a single target type, 
   /// and removing the overrule when the instance is disposed.
   /// </summary>
   /// <typeparam name="T">The Type to be overruled.</typeparam>

   public abstract class ObjectOverrule<T> : ObjectOverrule where T : DBObject
   {
      bool enabled = false;
      static RXClass targetClass = RXObject.GetClass(typeof(T));
      bool isDisposing = false;

      public ObjectOverrule(bool enabled = true)
      {
         this.IsOverruling = enabled;
      }

      /// <summary>
      /// This property can be used to enable/disable
      /// overruling for the instance. 
      /// 
      /// The static Overruling property of the Overrule
      /// base type does nothing (it was disabled because
      /// it was enabling/disabling all overrules, rather
      /// than a specific overrule).
      /// 
      /// </summary>

      public virtual bool IsOverruling
      {
         get
         {
            return this.enabled;
         }
         set
         {
            if(this.enabled ^ value)
            {
               this.enabled = value;
               if(value)
                  AddOverrule(targetClass, this, true);
               else
                  RemoveOverrule(targetClass, this);
               OnEnabledChanged(this.enabled);
            }
         }
      }

      protected virtual void OnEnabledChanged(bool enabled)
      {
      }

      protected bool IsDisposing => isDisposing;

      protected override void Dispose(bool disposing)
      {
         if(disposing)
         {
            isDisposing = true;
            IsOverruling = false;
         }
         base.Dispose(disposing);
      }
   }

   public static partial class StringExtensions
   {

      public static bool Matches(this string str, string pattern, bool ignoreCase = true, bool regex = false)
      {
         if(str == null)
            return false;
         if(regex)
            return Regex.IsMatch(str, pattern,
               ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
         else
            return Utils.WcMatchEx(str, pattern, ignoreCase);
      }
   }
}
