﻿
namespace Lacjam.WebApi.UnitTests

open NSubstitute
open System

[<AutoOpen>]
module ActionFunc =

  /// converts a lambda from unit to unit to a System.Action
  let action f = Action(f)
  /// converts a lambda from a 'a to unit to a System.Action<'a>
  let action1 f = Action<_>(f)
  /// converts a lambda a System.Action
  let action2 f = Action<_,_>(f)
  /// converts a lambda a System.Action
  let action3 f = Action<_,_,_>(f)
  /// converts a lambda a System.Action
  let action4 f = Action<_,_,_,_>(f)
  
  /// converts a lambda to a System.Action
  let actionExn f = Action<exn>(f)
  
  /// converts a lambda from unit to 'a to System.Func<'a>
  let func f = Func<_>(f)
  /// converts a lambda from 'a to 'b to System.Func<'a,'b>
  let func1 f = Func<_,_>(f)
  /// converts a lambda with given params to and output 'x to a System.Func that returns 'x
  let func2 f = Func<_,_,_>(f)
  /// converts a lambda with given params to and output 'x to a System.Func that returns 'x
  let func3 f = Func<_,_,_,_>(f)
  /// converts a lambda with given params to and output 'x to a System.Func that returns 'x
  let func4 f = Func<_,_,_,_,_>(f)

[<AutoOpen>]
module NSubstituteExtensions =


  let any<'a> = Arg.Any<'a>()
  let is<'a> (value : 'a) = Arg.Is<'a>(value)

  let fake<'a when 'a: not struct> = Substitute.For<'a>()

  let clearReceivedCalls substitute = SubstituteExtensions.ClearReceivedCalls(substitute)

  let received substitute = SubstituteExtensions.Received(substitute)
  let didNotReceive substitute = SubstituteExtensions.DidNotReceive(substitute)

  let returns (arg : 'a) call = SubstituteExtensions.Returns(call, arg)
  
  let whenReceived f substitute = SubstituteExtensions.When(substitute, action1(f))