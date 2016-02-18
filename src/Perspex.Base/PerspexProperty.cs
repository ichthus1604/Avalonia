﻿// Copyright (c) The Perspex Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Reflection;
using Perspex.Data;
using Perspex.Utilities;

namespace Perspex
{
    /// <summary>
    /// Base class for perspex properties.
    /// </summary>
    public class PerspexProperty : IEquatable<PerspexProperty>
    {
        /// <summary>
        /// Represents an unset property value.
        /// </summary>
        public static readonly object UnsetValue = new Unset();

        private static int s_nextId = 1;
        private readonly int _id;
        private readonly Subject<PerspexPropertyChangedEventArgs> _initialized;
        private readonly Subject<PerspexPropertyChangedEventArgs> _changed;
        private readonly PropertyMetadata _defaultMetadata;
        private readonly Dictionary<Type, PropertyMetadata> _metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="PerspexProperty"/> class.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="valueType">The type of the property's value.</param>
        /// <param name="ownerType">The type of the class that registers the property.</param>
        /// <param name="metadata">The property metadata.</param>
        /// <param name="notifying">A <see cref="Notifying"/> callback.</param>
        protected PerspexProperty(
            string name,
            Type valueType,
            Type ownerType,
            PropertyMetadata metadata,
            Action<IPerspexObject, bool> notifying = null)
        {
            Contract.Requires<ArgumentNullException>(name != null);
            Contract.Requires<ArgumentNullException>(valueType != null);
            Contract.Requires<ArgumentNullException>(ownerType != null);
            Contract.Requires<ArgumentNullException>(metadata != null);

            if (name.Contains("."))
            {
                throw new ArgumentException("'name' may not contain periods.");
            }

            _initialized = new Subject<PerspexPropertyChangedEventArgs>();
            _changed = new Subject<PerspexPropertyChangedEventArgs>();
            _metadata = new Dictionary<Type, PropertyMetadata>();

            Name = name;
            PropertyType = valueType;
            OwnerType = ownerType;
            Notifying = notifying;
            _id = s_nextId++;

            _metadata.Add(ownerType, metadata);
            _defaultMetadata = metadata;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PerspexProperty"/> class.
        /// </summary>
        /// <param name="source">The direct property to copy.</param>
        /// <param name="ownerType">The new owner type.</param>
        protected PerspexProperty(PerspexProperty source, Type ownerType)
        {
            Contract.Requires<ArgumentNullException>(source != null);
            Contract.Requires<ArgumentNullException>(ownerType != null);

            _initialized = source._initialized;
            _changed = source._changed;
            _metadata = new Dictionary<Type, PropertyMetadata>();

            Name = source.Name;
            PropertyType = source.PropertyType;
            OwnerType = ownerType;
            Notifying = source.Notifying;
            _id = source._id;
            _defaultMetadata = source._defaultMetadata;
        }

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of the property's value.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// Gets the type of the class that registered the property.
        /// </summary>
        public Type OwnerType { get; }

        /// <summary>
        /// Gets a value indicating whether the property inherits its value.
        /// </summary>
        public virtual bool Inherits => false;

        /// <summary>
        /// Gets a value indicating whether this is an attached property.
        /// </summary>
        public virtual bool IsAttached => false;

        /// <summary>
        /// Gets a value indicating whether this is a direct property.
        /// </summary>
        public virtual bool IsDirect => false;

        /// <summary>
        /// Gets a value indicating whether this is a readonly property.
        /// </summary>
        public virtual bool IsReadOnly => false;

        /// <summary>
        /// Gets an observable that is fired when this property is initialized on a
        /// new <see cref="PerspexObject"/> instance.
        /// </summary>
        /// <remarks>
        /// This observable is fired each time a new <see cref="PerspexObject"/> is constructed
        /// for all properties registered on the object's type. The default value of the property
        /// for the object is passed in the args' NewValue (OldValue will always be
        /// <see cref="UnsetValue"/>.
        /// </remarks>
        /// <value>
        /// An observable that is fired when this property is initialized on a new
        /// <see cref="PerspexObject"/> instance.
        /// </value>
        public IObservable<PerspexPropertyChangedEventArgs> Initialized => _initialized;

        /// <summary>
        /// Gets an observable that is fired when this property changes on any
        /// <see cref="PerspexObject"/> instance.
        /// </summary>
        /// <value>
        /// An observable that is fired when this property changes on any
        /// <see cref="PerspexObject"/> instance.
        /// </value>
        public IObservable<PerspexPropertyChangedEventArgs> Changed => _changed;

        /// <summary>
        /// Gets a method that gets called before and after the property starts being notified on an
        /// object.
        /// </summary>
        /// <remarks>
        /// When a property changes, change notifications are sent to all property subscribers; 
        /// for example via the <see cref="PerspexProperty.Changed"/> observable and and the 
        /// <see cref="PerspexObject.PropertyChanged"/> event. If this callback is set for a property,
        /// then it will be called before and after these notifications take place. The bool argument
        /// will be true before the property change notifications are sent and false afterwards. This 
        /// callback is intended to support Control.IsDataContextChanging.
        /// </remarks>
        public Action<IPerspexObject, bool> Notifying { get; }

        /// <summary>
        /// Provides access to a property's binding via the <see cref="PerspexObject"/>
        /// indexer.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>A <see cref="IndexerDescriptor"/> describing the binding.</returns>
        public static IndexerDescriptor operator !(PerspexProperty property)
        {
            return new IndexerDescriptor
            {
                Priority = BindingPriority.LocalValue,
                Property = property,
            };
        }

        /// <summary>
        /// Provides access to a property's template binding via the <see cref="PerspexObject"/>
        /// indexer.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>A <see cref="IndexerDescriptor"/> describing the binding.</returns>
        public static IndexerDescriptor operator ~(PerspexProperty property)
        {
            return new IndexerDescriptor
            {
                Priority = BindingPriority.TemplatedParent,
                Property = property,
            };
        }

        /// <summary>
        /// Tests two <see cref="PerspexProperty"/>s for equality.
        /// </summary>
        /// <param name="a">The first property.</param>
        /// <param name="b">The second property.</param>
        /// <returns>True if the properties are equal, otherwise false.</returns>
        public static bool operator ==(PerspexProperty a, PerspexProperty b)
        {
            if (object.ReferenceEquals(a, b))
            {
                return true;
            }
            else if (((object)a == null) || ((object)b == null))
            {
                return false;
            }
            else
            {
                return a.Equals(b);
            }
        }

        /// <summary>
        /// Tests two <see cref="PerspexProperty"/>s for unequality.
        /// </summary>
        /// <param name="a">The first property.</param>
        /// <param name="b">The second property.</param>
        /// <returns>True if the properties are equal, otherwise false.</returns>
        public static bool operator !=(PerspexProperty a, PerspexProperty b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Registers a <see cref="PerspexProperty"/>.
        /// </summary>
        /// <typeparam name="TOwner">The type of the class that is registering the property.</typeparam>
        /// <typeparam name="TValue">The type of the property's value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="defaultValue">The default value of the property.</param>
        /// <param name="inherits">Whether the property inherits its value.</param>
        /// <param name="defaultBindingMode">The default binding mode for the property.</param>
        /// <param name="validate">A validation function.</param>
        /// <param name="notifying">
        /// A method that gets called before and after the property starts being notified on an
        /// object; the bool argument will be true before and false afterwards. This callback is
        /// intended to support IsDataContextChanging.
        /// </param>
        /// <returns>A <see cref="StyledProperty{TValue}"/></returns>
        public static StyledProperty<TValue> Register<TOwner, TValue>(
            string name,
            TValue defaultValue = default(TValue),
            bool inherits = false,
            BindingMode defaultBindingMode = BindingMode.OneWay,
            Func<TOwner, TValue, TValue> validate = null,
            Action<IPerspexObject, bool> notifying = null)
                where TOwner : IPerspexObject
        {
            Contract.Requires<ArgumentNullException>(name != null);

            var metadata = new StyledPropertyMetadata<TValue>(
                defaultValue,
                validate: Cast(validate),
                defaultBindingMode: defaultBindingMode);

            var result = new StyledProperty<TValue>(
                name, 
                typeof(TOwner), 
                metadata,
                inherits,
                notifying);
            PerspexPropertyRegistry.Instance.Register(typeof(TOwner), result);
            return result;
        }

        /// <summary>
        /// Registers an attached <see cref="PerspexProperty"/>.
        /// </summary>
        /// <typeparam name="TOwner">The type of the class that is registering the property.</typeparam>
        /// <typeparam name="THost">The type of the class that the property is to be registered on.</typeparam>
        /// <typeparam name="TValue">The type of the property's value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="defaultValue">The default value of the property.</param>
        /// <param name="inherits">Whether the property inherits its value.</param>
        /// <param name="defaultBindingMode">The default binding mode for the property.</param>
        /// <param name="validate">A validation function.</param>
        /// <returns>A <see cref="PerspexProperty{TValue}"/></returns>
        public static AttachedProperty<TValue> RegisterAttached<TOwner, THost, TValue>(
            string name,
            TValue defaultValue = default(TValue),
            bool inherits = false,
            BindingMode defaultBindingMode = BindingMode.OneWay,
            Func<THost, TValue, TValue> validate = null)
                where THost : IPerspexObject
        {
            Contract.Requires<ArgumentNullException>(name != null);

            var metadata = new StyledPropertyMetadata<TValue>(
                defaultValue,
                validate: Cast(validate),
                defaultBindingMode: defaultBindingMode);

            var result = new AttachedProperty<TValue>(name, typeof(TOwner), metadata, inherits);
            PerspexPropertyRegistry.Instance.Register(typeof(THost), result);
            return result;
        }

        /// <summary>
        /// Registers an attached <see cref="PerspexProperty"/>.
        /// </summary>
        /// <typeparam name="THost">The type of the class that the property is to be registered on.</typeparam>
        /// <typeparam name="TValue">The type of the property's value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="ownerType">The type of the class that is registering the property.</param>
        /// <param name="defaultValue">The default value of the property.</param>
        /// <param name="inherits">Whether the property inherits its value.</param>
        /// <param name="defaultBindingMode">The default binding mode for the property.</param>
        /// <param name="validate">A validation function.</param>
        /// <returns>A <see cref="PerspexProperty{TValue}"/></returns>
        public static AttachedProperty<TValue> RegisterAttached<THost, TValue>(
            string name,
            Type ownerType,
            TValue defaultValue = default(TValue),
            bool inherits = false,
            BindingMode defaultBindingMode = BindingMode.OneWay,
            Func<THost, TValue, TValue> validate = null)
                where THost : IPerspexObject
        {
            Contract.Requires<ArgumentNullException>(name != null);

            var metadata = new StyledPropertyMetadata<TValue>(
                defaultValue,
                validate: Cast(validate),
                defaultBindingMode: defaultBindingMode);

            var result = new AttachedProperty<TValue>(name, ownerType, metadata, inherits);
            PerspexPropertyRegistry.Instance.Register(typeof(THost), result);
            return result;
        }

        /// <summary>
        /// Registers a direct <see cref="PerspexProperty"/>.
        /// </summary>
        /// <typeparam name="TOwner">The type of the class that is registering the property.</typeparam>
        /// <typeparam name="TValue">The type of the property's value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="getter">Gets the current value of the property.</param>
        /// <param name="setter">Sets the value of the property.</param>
        /// <param name="defaultBindingMode">The default binding mode for the property.</param>
        /// <returns>A <see cref="PerspexProperty{TValue}"/></returns>
        public static DirectProperty<TOwner, TValue> RegisterDirect<TOwner, TValue>(
            string name,
            Func<TOwner, TValue> getter,
            Action<TOwner, TValue> setter = null,
            BindingMode defaultBindingMode = BindingMode.OneWay)
                where TOwner : IPerspexObject
        {
            Contract.Requires<ArgumentNullException>(name != null);

            var metadata = new PropertyMetadata(defaultBindingMode);
            var result = new DirectProperty<TOwner, TValue>(name, getter, setter, metadata);
            PerspexPropertyRegistry.Instance.Register(typeof(TOwner), result);
            return result;
        }

        /// <summary>
        /// Returns a binding accessor that can be passed to <see cref="PerspexObject"/>'s []
        /// operator to initiate a binding.
        /// </summary>
        /// <returns>A <see cref="IndexerDescriptor"/>.</returns>
        /// <remarks>
        /// The ! and ~ operators are short forms of this.
        /// </remarks>
        public IndexerDescriptor Bind()
        {
            return new IndexerDescriptor
            {
                Property = this,
            };
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var p = obj as PerspexProperty;
            return p != null && Equals(p);
        }

        /// <inheritdoc/>
        public bool Equals(PerspexProperty other)
        {
            return other != null && _id == other._id;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _id;
        }

        /// <summary>
        /// Gets the property metadata for the specified type.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>
        /// The property metadata.
        /// </returns>
        public PropertyMetadata GetMetadata<T>() where T : IPerspexObject
        {
            var type = typeof(T);

            while (type != null)
            {
                PropertyMetadata result;

                if (_metadata.TryGetValue(type, out result))
                {
                    return result;
                }

                type = type.GetTypeInfo().BaseType;
            }

            return _defaultMetadata;
        }


        /// <summary>
        /// Gets the property metadata for the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>
        /// The property metadata.
        /// </returns>
        public PropertyMetadata GetMetadata(Type type)
        {
            Contract.Requires<ArgumentNullException>(type != null);

            while (type != null)
            {
                PropertyMetadata result;

                if (_metadata.TryGetValue(type, out result))
                {
                    return result;
                }

                type = type.GetTypeInfo().BaseType;
            }

            return _defaultMetadata;
        }

        /// <summary>
        /// Checks whether the <paramref name="value"/> is valid for the property.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True if the value is valid, otherwise false.</returns>
        public bool IsValidValue(object value)
        {
            return TypeUtilities.TryCast(PropertyType, value, out value);
        }

        /// <summary>
        /// Gets the string representation of the property.
        /// </summary>
        /// <returns>The property's string representation.</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Notifies the <see cref="Initialized"/> observable.
        /// </summary>
        /// <param name="e">The observable arguments.</param>
        internal void NotifyInitialized(PerspexPropertyChangedEventArgs e)
        {
            _initialized.OnNext(e);
        }

        /// <summary>
        /// Notifies the <see cref="Changed"/> observable.
        /// </summary>
        /// <param name="e">The observable arguments.</param>
        internal void NotifyChanged(PerspexPropertyChangedEventArgs e)
        {
            _changed.OnNext(e);
        }

        /// <summary>
        /// Overrides the metadata for the property on the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="metadata">The metadata.</param>
        protected void OverrideMetadata(Type type, PropertyMetadata metadata)
        {
            Contract.Requires<ArgumentNullException>(type != null);
            Contract.Requires<ArgumentNullException>(metadata != null);

            if (_metadata.ContainsKey(type))
            {
                throw new InvalidOperationException(
                    $"Metadata is already set for {Name} on {type}.");
            }

            var baseMetadata = GetMetadata(type);
            metadata.Merge(baseMetadata, this);
            _metadata.Add(type, metadata);
        }

        [DebuggerHidden]
        private static Func<IPerspexObject, TValue, TValue> Cast<TOwner, TValue>(Func<TOwner, TValue, TValue> f)
            where TOwner : IPerspexObject
        {
            if (f != null)
            {
                return (o, v) => (o is TOwner) ? f((TOwner)o, v) : v;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Class representing the <see cref="UnsetValue"/>.
        /// </summary>
        private class Unset
        {
            /// <summary>
            /// Returns the string representation of the <see cref="UnsetValue"/>.
            /// </summary>
            /// <returns>The string "(unset)".</returns>
            public override string ToString() => "(unset)";
        }
    }
}
