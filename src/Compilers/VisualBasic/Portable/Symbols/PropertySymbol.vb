﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Represents a property.
    ''' </summary>
    Friend MustInherit Class PropertySymbol
        Inherits Symbol
        Implements IPropertySymbol

        ' !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        ' Changes to the public interface of this class should remain synchronized with the C# version.
        ' Do not make any changes to the public interface without making the corresponding change
        ' to the C# version.
        ' !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        Friend Sub New()
        End Sub

        ''' <summary>
        ''' Get the original definition of this symbol. If this symbol is derived from another
        ''' symbol by (say) type substitution, this gets the original symbol, as it was defined
        ''' in source or metadata.
        ''' </summary>
        Public Overridable Shadows ReadOnly Property OriginalDefinition As PropertySymbol
            Get
                ' Default implements returns Me.
                Return Me
            End Get
        End Property

        Protected NotOverridable Overrides ReadOnly Property OriginalSymbolDefinition As Symbol
            Get
                Return Me.OriginalDefinition
            End Get
        End Property

        ''' <summary>
        ''' Source: Returns False; properties from source cannot return by reference.
        ''' Metadata: Returns whether or not this property returns by reference.
        ''' </summary>
        Public MustOverride ReadOnly Property ReturnsByRef As Boolean

        ''' <summary>
        ''' Gets the type of the property. 
        ''' </summary>
        Public MustOverride ReadOnly Property Type As TypeSymbol

        ''' <summary>
        ''' Returns the list of custom modifiers, if any, associated with the type of the property. 
        ''' </summary>
        Public MustOverride ReadOnly Property TypeCustomModifiers As ImmutableArray(Of CustomModifier)

        ''' <summary>
        ''' Custom modifiers associated with the ref modifier, or an empty array if there are none.
        ''' </summary>
        Public MustOverride ReadOnly Property RefCustomModifiers As ImmutableArray(Of CustomModifier)

        ''' <summary>
        ''' Gets the parameters of this property. If this property has no parameters, returns
        ''' an empty list.
        ''' </summary>
        Public MustOverride ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)

        ''' <summary>
        ''' Optimization: in many cases, the parameter count (fast) is sufficient and we
        ''' don't need the actual parameter symbols (slow).
        ''' </summary>
        ''' <remarks>
        ''' The default implementation is always correct, but may be unnecessarily slow.
        ''' </remarks>
        Public Overridable ReadOnly Property ParameterCount As Integer
            Get
                Return Me.Parameters.Length
            End Get
        End Property

        ''' <summary>
        ''' True if the property itself Is excluded from code coverage instrumentation.
        ''' True for source properties marked with <see cref="AttributeDescription.ExcludeFromCodeCoverageAttribute"/>.
        ''' </summary>
        Friend Overridable ReadOnly Property IsDirectlyExcludedFromCodeCoverage As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        '''  True if this symbol has a special name (metadata flag SpecialName is set).
        ''' </summary>
        Friend MustOverride ReadOnly Property HasSpecialName As Boolean

        ''' <summary>
        ''' Returns true if this property is a default property. 
        ''' </summary>
        Public MustOverride ReadOnly Property IsDefault As Boolean Implements IPropertySymbol.IsIndexer

        ''' <summary>
        ''' Returns true if this is a read-only property; i.e., has no set accessor.
        ''' </summary>
        Public Overridable ReadOnly Property IsReadOnly As Boolean Implements IPropertySymbol.IsReadOnly
            Get
                Return (Me.SetMethod Is Nothing)
            End Get
        End Property

        ''' <summary>
        ''' Indicates if the property can be read, which means this 
        ''' type overrides OR inherits a getter for this property.
        ''' </summary>
        Friend ReadOnly Property IsReadable As Boolean
            Get
                Return Me.GetMostDerivedGetMethod() IsNot Nothing
            End Get
        End Property

        ''' <summary>
        ''' Returns true if this is a write-only property; i.e., has no get accessor.
        ''' </summary>
        Public Overridable ReadOnly Property IsWriteOnly As Boolean Implements IPropertySymbol.IsWriteOnly
            Get
                Return (Me.GetMethod Is Nothing)
            End Get
        End Property

        ''' <summary>
        ''' Indicates if the property has a Set accessor.
        ''' </summary>
        Friend ReadOnly Property HasSet As Boolean
            Get
                Return Me.GetMostDerivedSetMethod() IsNot Nothing
            End Get
        End Property

        ''' <summary>
        ''' Indicates if the property can be written into, which means this 
        ''' property has a setter or it is a getter only autoproperty accessed 
        ''' in a corresponding constructor or initializer.
        ''' If the setter is init-only, we also check that it is accessed in a constructor
        ''' on Me/MyBase/MyClass or is a target of a member initializer in an object member
        ''' initializer.
        ''' </summary>
        Friend Function IsWritable(receiverOpt As BoundExpression, containingBinder As Binder, isKnownTargetOfObjectMemberInitializer As Boolean) As Boolean
            Debug.Assert(containingBinder IsNot Nothing)

            Dim mostDerivedSet As MethodSymbol = Me.GetMostDerivedSetMethod()

            If mostDerivedSet IsNot Nothing Then
                If Not mostDerivedSet.IsInitOnly Then
                    Return True
                End If

                If receiverOpt Is Nothing Then
                    Return False
                End If

                ' ok: New C() With { .InitOnlyProperty = ... }
                If isKnownTargetOfObjectMemberInitializer Then
                    Debug.Assert(receiverOpt.Kind = BoundKind.WithLValueExpressionPlaceholder)
                    Return True
                End If

                ' ok: setting on `Me`/`MyBase`/`MyClass` from an instance constructor
                Dim containingMember As Symbol = containingBinder.ContainingMember
                If If(TryCast(containingMember, MethodSymbol)?.MethodKind <> MethodKind.Constructor, True) Then
                    Return False
                End If

                If receiverOpt.Kind = BoundKind.WithLValueExpressionPlaceholder OrElse receiverOpt.Kind = BoundKind.WithRValueExpressionPlaceholder Then
                    ' This can be a reference used as a target for a `With` statement
                    Dim currentBinder As Binder = containingBinder

                    While currentBinder IsNot Nothing AndAlso currentBinder.ContainingMember Is containingMember
                        Dim withBlockBinder = TryCast(currentBinder, WithBlockBinder)
                        If withBlockBinder IsNot Nothing Then
                            If withBlockBinder.Info?.ExpressionPlaceholder Is receiverOpt Then
                                receiverOpt = withBlockBinder.Info.OriginalExpression
                            End If

                            Exit While
                        End If

                        currentBinder = currentBinder.ContainingBinder
                    End While
                End If

                Do
                    Select Case receiverOpt.Kind
                        Case BoundKind.MeReference, BoundKind.MyBaseReference, BoundKind.MyClassReference
                            Return True
                        Case BoundKind.Parenthesized
                            receiverOpt = DirectCast(receiverOpt, BoundParenthesized).Expression
                        Case Else
                            Return False
                    End Select
                Loop
            End If

            Dim sourceProperty As SourcePropertySymbol = TryCast(Me, SourcePropertySymbol)
            Dim propertyIsStatic As Boolean = Me.IsShared
            Dim fromMember = containingBinder.ContainingMember

            Return sourceProperty IsNot Nothing AndAlso fromMember IsNot Nothing AndAlso
                sourceProperty.IsAutoProperty AndAlso
                TypeSymbol.Equals(sourceProperty.ContainingType, fromMember.ContainingType, TypeCompareKind.ConsiderEverything) AndAlso
                propertyIsStatic = fromMember.IsShared AndAlso
                (propertyIsStatic OrElse (receiverOpt IsNot Nothing AndAlso receiverOpt.Kind = BoundKind.MeReference)) AndAlso
                ((fromMember.Kind = SymbolKind.Method AndAlso DirectCast(fromMember, MethodSymbol).IsAnyConstructor) OrElse
                        TypeOf containingBinder Is DeclarationInitializerBinder)

        End Function


        ''' <summary>
        ''' Gets the associated "get" method for this property. If this property
        ''' has no get accessor, returns Nothing.
        ''' </summary>
        Public MustOverride ReadOnly Property GetMethod As MethodSymbol

        ''' <summary>
        ''' Retrieves Get method for this property or 'most derived' Get method from closest 
        ''' overridden property if such property exists.
        ''' 
        ''' NOTE: It is not possible in VB, but possible in other languages (for example in C#) to
        '''       override read-write property and provide override only for setter, thus inheriting 
        '''       getter's implementation. This method will find the Get method from the most-derived
        '''       overridden property in this case
        ''' </summary>
        Friend Function GetMostDerivedGetMethod() As MethodSymbol
            Dim [property] = Me
            Do
                Dim getMethod = [property].GetMethod
                If getMethod IsNot Nothing Then
                    Return getMethod
                End If
                [property] = [property].OverriddenProperty
            Loop Until [property] Is Nothing
            Return Nothing
        End Function

        ''' <summary>
        ''' Gets the associated "set" method for this property. If this property
        ''' has no set accessor, returns Nothing.
        ''' </summary>
        Public MustOverride ReadOnly Property SetMethod As MethodSymbol

        ''' <summary>
        ''' Retrieves Set method for this property or 'most derived' Set method from closest 
        ''' overridden property if such property exists.
        ''' 
        ''' NOTE: It is not possible in VB, but possible in other languages (for example in C#) to
        '''       override read-write property and provide override only for getter, thus inheriting 
        '''       setter's implementation. This method will find the Set method from the most-derived
        '''       overridden property in this case
        ''' </summary>
        Friend Function GetMostDerivedSetMethod() As MethodSymbol
            Dim [property] = Me
            Do
                Dim setMethod = [property].SetMethod
                If setMethod IsNot Nothing Then
                    Return setMethod
                End If
                [property] = [property].OverriddenProperty
            Loop Until [property] Is Nothing
            Return Nothing
        End Function

        ''' <summary>
        ''' Backing field of the property, or Nothing if the property doesn't have any.
        ''' </summary>
        ''' <remarks>
        ''' Properties imported from metadata return Nothing.
        ''' </remarks>
        Friend MustOverride ReadOnly Property AssociatedField As FieldSymbol

        ''' <summary>
        ''' Gets the attributes on event's associated field, if any.
        ''' </summary>
        ''' <returns>Returns an array of <see cref="VisualBasicAttributeData"/> or an empty array if there are no attributes.</returns>
        ''' <remarks>
        ''' Only WithEvent property may have any attributes applied on its backing field.
        ''' </remarks>
        Public Function GetFieldAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            Dim field = Me.AssociatedField
            Return If(field Is Nothing, ImmutableArray(Of VisualBasicAttributeData).Empty, field.GetAttributes())
        End Function

        ''' <summary>
        ''' Returns true if this property hides a base property by name and signature.
        ''' The equivalent of the "hidebysig" flag in metadata. 
        ''' </summary>
        ''' <remarks>
        ''' This property should not be confused with general property overloading in Visual Basic, and is not directly related. 
        ''' This property will only return true if this method hides a base property by name and signature (Overloads keyword).
        ''' </remarks>
        Public MustOverride ReadOnly Property IsOverloads As Boolean

        ''' <summary>
        ''' If this property overrides another property (because it both had the Overrides modifier
        ''' and there correctly was a property to override), returns the overridden property.
        ''' </summary>
        Public ReadOnly Property OverriddenProperty As PropertySymbol
            Get
                If Me.IsOverrides Then
                    If IsDefinition Then
                        Return OverriddenMembers.OverriddenMember
                    End If

                    Return OverriddenMembersResult(Of PropertySymbol).GetOverriddenMember(Me, Me.OriginalDefinition.OverriddenProperty)
                End If

                Return Nothing
            End Get
        End Property

        ''' <summary>
        ''' Helper method for accessors to get the overridden accessor methods. Should only be called by the
        ''' accessor method symbols.
        ''' </summary>
        ''' <param name="getter">True to get overridden getters, False to get overridden setters</param>
        ''' <returns>All the accessors of the given kind implemented by this property.</returns>
        Friend Function GetAccessorOverride(getter As Boolean) As MethodSymbol
            Dim overriddenProp = Me.OverriddenProperty
            If overriddenProp IsNot Nothing Then
                Return If(getter, overriddenProp.GetMethod, overriddenProp.SetMethod)
            Else
                Return Nothing
            End If
        End Function

        ' Get the set of overridden and hidden members for this property.
        Friend Overridable ReadOnly Property OverriddenMembers As OverriddenMembersResult(Of PropertySymbol)
            Get
                ' To save space, the default implementation does not cache its result.  We expect there to
                ' be a very large number of MethodSymbols and we expect that a large percentage of them will
                ' obviously not override anything (e.g. static methods, constructors, destructors, etc).
                Return OverrideHidingHelper(Of PropertySymbol).MakeOverriddenMembers(Me)
            End Get
        End Property

        ''' <summary>
        ''' Returns interface properties explicitly implemented by this property.
        ''' </summary>
        Public MustOverride ReadOnly Property ExplicitInterfaceImplementations As ImmutableArray(Of PropertySymbol)

        Public NotOverridable Overrides ReadOnly Property Kind As SymbolKind
            Get
                Return SymbolKind.Property
            End Get
        End Property

        Friend MustOverride ReadOnly Property CallingConvention As Microsoft.Cci.CallingConvention

        Friend Overrides Function Accept(Of TArgument, TResult)(visitor As VisualBasicSymbolVisitor(Of TArgument, TResult), arg As TArgument) As TResult
            Return visitor.VisitProperty(Me, arg)
        End Function

        ''' <summary>
        ''' Get the "this" parameter for this property.  This is only valid for source fields.
        ''' </summary>
        Friend Overridable ReadOnly Property MeParameter As ParameterSymbol
            Get
                Throw ExceptionUtilities.Unreachable
            End Get
        End Property

        Friend Overridable ReadOnly Property ReducedFrom As PropertySymbol
            Get
                Return Nothing
            End Get
        End Property

        Friend Overridable ReadOnly Property ReducedFromDefinition As PropertySymbol
            Get
                Return Nothing
            End Get
        End Property

        Friend Overridable ReadOnly Property ReceiverType As TypeSymbol
            Get
                Return ContainingType
            End Get
        End Property

        Friend Overrides Function GetUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)
            If Me.IsDefinition Then
                Return New UseSiteInfo(Of AssemblySymbol)(PrimaryDependency)
            End If

            Return Me.OriginalDefinition.GetUseSiteInfo()
        End Function

        Friend Function CalculateUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)

            Debug.Assert(IsDefinition)

            ' Check return type.
            Dim useSiteInfo As UseSiteInfo(Of AssemblySymbol) = New UseSiteInfo(Of AssemblySymbol)(Me.PrimaryDependency)

            If MergeUseSiteInfo(useSiteInfo, DeriveUseSiteInfoFromType(Me.Type)) Then
                Return useSiteInfo
            End If

            ' Check return type custom modifiers.
            Dim refModifiersUseSiteInfo = DeriveUseSiteInfoFromCustomModifiers(Me.RefCustomModifiers)

            If MergeUseSiteInfo(useSiteInfo, refModifiersUseSiteInfo) Then
                Return useSiteInfo
            End If

            Dim typeModifiersUseSiteInfo = DeriveUseSiteInfoFromCustomModifiers(Me.TypeCustomModifiers)

            If MergeUseSiteInfo(useSiteInfo, typeModifiersUseSiteInfo) Then
                Return useSiteInfo
            End If

            ' Check parameters.
            Dim parametersUseSiteInfo = DeriveUseSiteInfoFromParameters(Me.Parameters)

            If MergeUseSiteInfo(useSiteInfo, parametersUseSiteInfo) Then
                Return useSiteInfo
            End If

            Dim errorInfo As DiagnosticInfo = useSiteInfo.DiagnosticInfo

            ' If the member is in an assembly with unified references, 
            ' we check if its definition depends on a type from a unified reference.
            If errorInfo Is Nothing AndAlso Me.ContainingModule.HasUnifiedReferences Then
                Dim unificationCheckedTypes As HashSet(Of TypeSymbol) = Nothing
                errorInfo = If(Me.Type.GetUnificationUseSiteDiagnosticRecursive(Me, unificationCheckedTypes),
                            If(GetUnificationUseSiteDiagnosticRecursive(Me.RefCustomModifiers, Me, unificationCheckedTypes),
                            If(GetUnificationUseSiteDiagnosticRecursive(Me.TypeCustomModifiers, Me, unificationCheckedTypes),
                               GetUnificationUseSiteDiagnosticRecursive(Me.Parameters, Me, unificationCheckedTypes))))

                Debug.Assert(errorInfo Is Nothing OrElse errorInfo.Severity = DiagnosticSeverity.Error)
            End If

            If errorInfo IsNot Nothing Then
                Return New UseSiteInfo(Of AssemblySymbol)(errorInfo)
            End If

            Dim primaryDependency = useSiteInfo.PrimaryDependency
            Dim secondaryDependency = useSiteInfo.SecondaryDependencies

            refModifiersUseSiteInfo.MergeDependencies(primaryDependency, secondaryDependency)
            typeModifiersUseSiteInfo.MergeDependencies(primaryDependency, secondaryDependency)
            parametersUseSiteInfo.MergeDependencies(primaryDependency, secondaryDependency)

            Return New UseSiteInfo(Of AssemblySymbol)(diagnosticInfo:=Nothing, primaryDependency, secondaryDependency)
        End Function

        ''' <summary>
        ''' Return error code that has highest priority while calculating use site error for this symbol. 
        ''' </summary>
        Protected Overrides Function IsHighestPriorityUseSiteError(code As Integer) As Boolean
            Return code = ERRID.ERR_UnsupportedProperty1 OrElse code = ERRID.ERR_UnsupportedCompilerFeature
        End Function

        Public NotOverridable Overrides ReadOnly Property HasUnsupportedMetadata As Boolean
            Get
                Dim info As DiagnosticInfo = GetUseSiteInfo().DiagnosticInfo
                Return info IsNot Nothing AndAlso (info.Code = ERRID.ERR_UnsupportedProperty1 OrElse info.Code = ERRID.ERR_UnsupportedCompilerFeature)
            End Get
        End Property

        ''' <summary>
        ''' Returns true if this property is an auto-created WithEvents property that 
        ''' takes place of a field member when the field is marked as WithEvents.
        ''' </summary>
        Public Overridable ReadOnly Property IsWithEvents As Boolean Implements IPropertySymbol.IsWithEvents
            Get
                Dim overridden = Me.OverriddenProperty
                If overridden Is Nothing Then
                    Return False
                End If

                Return overridden.IsWithEvents
            End Get
        End Property

        Friend Overrides ReadOnly Property EmbeddedSymbolKind As EmbeddedSymbolKind
            Get
                Return Me.ContainingSymbol.EmbeddedSymbolKind
            End Get
        End Property

        ''' <summary>
        ''' Is this a property of a tuple type?
        ''' </summary>
        Public Overridable ReadOnly Property IsTupleProperty() As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' If this is a property of a tuple type, return corresponding underlying property from the
        ''' tuple underlying type. Otherwise, Nothing. 
        ''' </summary>
        Public Overridable ReadOnly Property TupleUnderlyingProperty() As PropertySymbol
            Get
                Return Nothing
            End Get
        End Property

        ''' <summary>
        ''' Clone the property parameters for the accessor method. The
        ''' parameters are cloned (rather than referenced from the property)
        ''' since the ContainingSymbol needs to be set to the accessor.
        ''' </summary>
        Friend Sub CloneParameters(method As MethodSymbol, parameters As ArrayBuilder(Of ParameterSymbol))
            Dim originalParameters = Me.Parameters

            For i As Integer = 0 To originalParameters.Length - 1
                Dim propertyParameter = originalParameters(i)
                ' TODO: Should CustomModifiers be used from this property
                ' parameter even in the case of overridden properties?

                parameters.Add(propertyParameter.ChangeOwner(method))
            Next
        End Sub

        ''' <summary>
        ''' Is this an auto-generated property of a group class?
        ''' </summary>
        Friend MustOverride Overrides ReadOnly Property IsMyGroupCollectionProperty As Boolean

#Region "IPropertySymbol"

        Private ReadOnly Property IPropertySymbol_ExplicitInterfaceImplementations As ImmutableArray(Of IPropertySymbol) Implements IPropertySymbol.ExplicitInterfaceImplementations
            Get
                Return ImmutableArrayExtensions.Cast(Of PropertySymbol, IPropertySymbol)(Me.ExplicitInterfaceImplementations)
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_GetMethod As IMethodSymbol Implements IPropertySymbol.GetMethod
            Get
                Return Me.GetMethod
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_OriginalDefinition As IPropertySymbol Implements IPropertySymbol.OriginalDefinition
            Get
                Return Me.OriginalDefinition
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_OverriddenProperty As IPropertySymbol Implements IPropertySymbol.OverriddenProperty
            Get
                Return Me.OverriddenProperty
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_Parameters As ImmutableArray(Of IParameterSymbol) Implements IPropertySymbol.Parameters
            Get
                Return StaticCast(Of IParameterSymbol).From(Me.Parameters)
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_SetMethod As IMethodSymbol Implements IPropertySymbol.SetMethod
            Get
                Return Me.SetMethod
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_IsRequired As Boolean Implements IPropertySymbol.IsRequired
            Get
                Return False
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_ReturnsByRef As Boolean Implements IPropertySymbol.ReturnsByRef
            Get
                Return Me.ReturnsByRef
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_ByRefReturnIsReadonly As Boolean Implements IPropertySymbol.ReturnsByRefReadonly
            Get
                Return False
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_RefKind As RefKind Implements IPropertySymbol.RefKind
            Get
                Return If(Me.ReturnsByRef, RefKind.Ref, RefKind.None)
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_Type As ITypeSymbol Implements IPropertySymbol.Type
            Get
                Return Me.Type
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_NullableAnnotation As NullableAnnotation Implements IPropertySymbol.NullableAnnotation
            Get
                Return NullableAnnotation.None
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_RefCustomModifiers As ImmutableArray(Of CustomModifier) Implements IPropertySymbol.RefCustomModifiers
            Get
                Return Me.RefCustomModifiers
            End Get
        End Property

        Private ReadOnly Property IPropertySymbol_TypeCustomModifiers As ImmutableArray(Of CustomModifier) Implements IPropertySymbol.TypeCustomModifiers
            Get
                Return Me.TypeCustomModifiers
            End Get
        End Property

        Public Overrides Sub Accept(visitor As SymbolVisitor)
            visitor.VisitProperty(Me)
        End Sub

        Public Overrides Function Accept(Of TResult)(visitor As SymbolVisitor(Of TResult)) As TResult
            Return visitor.VisitProperty(Me)
        End Function

        Public Overrides Function Accept(Of TArgument, TResult)(visitor As SymbolVisitor(Of TArgument, TResult), argument As TArgument) As TResult
            Return visitor.VisitProperty(Me, argument)
        End Function

        Public Overrides Sub Accept(visitor As VisualBasicSymbolVisitor)
            visitor.VisitProperty(Me)
        End Sub

        Public Overrides Function Accept(Of TResult)(visitor As VisualBasicSymbolVisitor(Of TResult)) As TResult
            Return visitor.VisitProperty(Me)
        End Function

#End Region

    End Class
End Namespace
