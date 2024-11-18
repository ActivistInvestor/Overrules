
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;

#pragma warning disable CS0618 // Type or member is obsolete

/// BlockAttributeOverrule.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under terms of the MIT license.

namespace AcMgdLib.DatabaseServices
{

   public abstract class BlockAttributeOverrule : ObjectOverrule<BlockReference>
   {
      string pattern;
      Dictionary<ObjectId, bool> map;
      HashSet<string> tags;
      bool noTags = false;

      /// <summary>
      /// Constructor
      /// 
      /// Specify a wildcard pattern matching the names of
      /// one or more blocks, and zero or more strings that
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
         noTags = tags.Length == 0;
         if(!noTags)
            this.tags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
      }

      protected override void OnOverruling()
      {
         map = new Dictionary<ObjectId, bool>();
         base.OnOverruling();
      }

      /// <summary>
      /// Because there is a minor issue with newly-inserted blocks
      /// when the ATTDIA sysvar is 0, this can be set to false to
      /// disable acting on newly-inserted block references created
      /// by the -INSERT command.
      /// 
      /// The default value is false. Note that this property only 
      /// controls how the overrule behaves with the -INSERT command. 
      /// New block references created via other means including OLE 
      /// drag & drop, PASTECLIP, COPY, etc. are always acted on.
      /// </summary>

      public bool InsertEnabled { get;set; }

      /// <summary>
      /// This class is abstract and is designed to be reusable.
      /// It should require no changes (other than bug fixes or
      /// enhancements). 
      /// 
      /// Reusing this class is achieved by deriving a class from 
      /// it and overriding the Update() method. Within an override 
      /// of Update(), the AttributeReference and the BlockReference 
      /// arguments are open for write and can be modified. Overrides 
      /// of this method can modify either argument, but shouldn't 
      /// call methods that alter the open state of either argument, 
      /// such as Close() or Cancel().
      /// 
      /// If other objects must be accessed from an override of
      /// this method, those objects should only be accessed using 
      /// an OpenCloseTransaction. Do not under any circumstances
      /// call the TransactionManager's StartTransaction() method
      /// from an override of this method.
      /// </summary>
      /// <param name="att">The AttributeReference to be operated on</param>
      /// <param name="owner">The owning BlockReference</param>

      protected virtual void Update(AttributeReference att, BlockReference owner)
      {
      }

      /// <summary>
      /// A derived type can override this to provide
      /// more granular filtering of block references,
      /// but should in almost all cases, supermessage
      /// this and logically-operate on its result:
      /// </summary>
      /// <param name="br"></param>
      /// <returns>A value indicating if the given 
      /// BlockReference and its attributes should be 
      /// operated on.</returns>
      
      protected virtual bool IsMatch(BlockReference br)
      {
         /// Cache the ObjectIds and match result of each
         /// BlockTableRecord, which allows subsequent 
         /// match queries to be reduced to nothing but 
         /// a dictionary lookup. This cache is Database-
         /// agnostic, and stores ObjectIds from multiple 
         /// Databases:

         var btrId = br.DynamicBlockTableRecord;
         if(!map.TryGetValue(btrId, out bool result))
         {
            using(var btr = (BlockTableRecord) btrId.Open(OpenMode.ForRead))
            {
               result = map[btrId] = IsMatch(btr);
            }
         }
         return result;
      }

      /// <summary>
      /// Allows derived types to qualify/disqualify
      /// block references using criteria derived from
      /// the block reference's definition. For Dynamic
      /// block references, the dynamic block's defining
      /// BlockTableRecord is passed in as the argument, 
      /// rather than the anonymous block definition.
      /// 
      /// The result is cached and therefore, this method 
      /// is called only once for each BlockTableRecord,
      /// and its result is used for subsequent references 
      /// to the same block definition.
      /// 
      /// The default criteria is to disqualify blocks
      /// having no attributes, and match the name of
      /// the BlockTableRecord against the pattern that
      /// was supplied to the constructor, without case-
      /// sensitivity. For dynamic blocks, the name of 
      /// the dynamic block's defining BlockTableRecord
      /// is compared, rather than that of an anonymous
      /// BlockTableRecord.
      /// </summary>
      /// <param name="btr">The referenced BlockTableRecord</param>
      /// <returns>A value indicating if references to the
      /// BlockTableRecord should be operated on</returns>
      
      protected virtual bool IsMatch(BlockTableRecord btr)
      {
         return btr.HasAttributeDefinitions
            && btr.Name.Matches(pattern);
      }

      public sealed override void Close(DBObject obj)
      {
         try
         {
            if(obj.IsWriteEnabled
               && obj.IsReallyClosing
               && (InsertEnabled || AppUtils.ActiveCommand != "-INSERT")
               && !DBObject.IsCustomObject(obj.ObjectId)
               && !obj.Database.AutoDelete 
               && !obj.IsUndoing
               && !obj.IsErased
               && obj is BlockReference blkref
               && IsMatch(blkref))
            {
               var dbtrans = blkref.Database.TransactionManager.TopTransaction;
               bool local = dbtrans == null;
               var tr = dbtrans ?? new OpenCloseTransaction();
               try
               {
                  foreach(ObjectId id in blkref.AttributeCollection)
                  {
                     if(id.IsErased)
                        continue;
                     var attref = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);
                     if(noTags || tags.Contains(attref.Tag))
                     {
                        tr.GetObject(id, OpenMode.ForWrite, false, true);
                        try
                        {
                           Update(attref, blkref);
                        }
                        catch(System.Exception ex)
                        {
                           IsOverruling = false;
                           base.Close(obj);
                           UnhandledExceptionFilter.CerOrShowExceptionDialog(ex);
                           AppUtils.Write($"\nException in {GetType().Name}.Update(): overrule disabled.");
                           return;
                        }
                        finally
                        {
                           attref.DowngradeOpen();
                        }
                     }
                  }
                  if(local)
                     tr.Commit();
               }
               finally
               {
                  if(local)
                     tr.Dispose();
               }
            }
         }
         catch(Autodesk.AutoCAD.Runtime.Exception ex) 
         {
            // We will get here if a block is inserted,
            // and ATTDIA == 0. In that case, the first
            // attribute prompted for is open for write
            // and throws an exception when attempting 
            // to open it.
            
            AppUtils.Write($"\nFailed to update attribute (e{ex.ErrorStatus})");
         }
         catch(System.Exception ex)
         {
            // If we get here, there is another problem
            // that is not expected, so the best course
            // of action is to disable the overrule.
            IsOverruling = false;
            UnhandledExceptionFilter.CerOrShowExceptionDialog(ex);
            AppUtils.Write($"\n{GetType().Name} disabled."); 
         }
         base.Close(obj);
      }

   }

   /// <summary>
   /// A generic specialization of ObjectOverrule that automates 
   /// adding/removing the overrule from a single target type
   /// and removing the overrule when the instance is disposed.
   /// </summary>
   /// <typeparam name="T">The Type to be overruled.</typeparam>

   public abstract class ObjectOverrule<T> : ObjectOverrule where T : DBObject
   {
      bool enabled = false;
      static RXClass targetClass = RXObject.GetClass(typeof(T));
      bool isDisposing = false;
      bool initializing = true;
      bool initialized = false;

      public ObjectOverrule(bool enabled = true)
      {
         this.IsOverruling = enabled;
         initializing = false;
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
               {
                  AddOverrule(targetClass, this, true);
                  if(!initialized)
                  {
                     OnOverruling();
                     initialized = true;
                  }
               }
               else
                  RemoveOverrule(targetClass, this);
               OnEnabledChanged(this.enabled);
            }
         }
      }

      /// <summary>
      /// Called the first time the overrule is enabled.
      /// Derived types can use an override of this to
      /// do lazy initialization. This is helpful in use
      /// cases where the Overrule may never be enabled,
      /// or may not be enabled until some conditions are
      /// met. Derived types can defer allocations until
      /// they are necessary.
      /// </summary>
      
      protected virtual void OnOverruling()
      {
      }

      protected virtual void OnEnabledChanged(bool enabled)
      {
      }

      /// <summary>
      /// An override of OnEnabledChanged() can be called
      /// before the containing type's constructor has been
      /// called. This property will be true in that case.
      /// 
      /// Derived types can do lazy initialization from an
      /// override of OnEnabledChanged() when this property
      /// is true.
      /// </summary>
      
      protected bool IsInitializing => initializing;

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

   public static class AppUtils
   {
      public static void Write(string msg)
      {
         Application.DocumentManager.MdiActiveDocument?.
            Editor.WriteMessage($"\n{msg}");
      }

      public static string ActiveCommand
         => Application.DocumentManager.MdiActiveDocument?.CommandInProgress;

      public static Database ActiveDatabase
         => Application.DocumentManager.MdiActiveDocument?.Database;
   }


}
