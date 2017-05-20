using System;
using System.Collections.Generic;
using NUnit.Framework;
using NSubstitute;

[TestFixture]
public class NSubstituePractices
{

    #region Data & Setup

    public interface IQueueValue<T>
    {
        void Enqueue(T obj);
        T Dequeue();
        bool Remove(T obj);
        bool IsEmpty();
        T Peek();
        event Action OnChanged;
        IQueueValue<T> Clone(string id);
    }

    public interface ICalculator
    {
        int Multiply(int a, int b);
    }

    private static IQueueValue<int> _target;

    [SetUp]
    public static void Setup()
    {
        // Raise event only works for interface、abstract class
        // If possible, stick to substituting interfaces.
        _target = Substitute.For<IQueueValue<int>>();
    }

    #endregion


    #region Setting Return Value

    [Test]
    public static void Return_Conditionally_Sequence()
    {
        // Note that Return values cannot be configured for non-virtual/non-abstract members.
        _target.Remove(Arg.Any<int>()).Returns(true, true);

        // Returns() can also be called with multiple arguments to set up a sequence of return values.
        var a = _target.Remove(Arg.Is(0));

        Assert.AreEqual(true, _target.Remove(Arg.Is(0)));
    }

    [Test]
    public static void ReturnForAnyArgs_AndDoes()
    {
        var c = 0;

        _target.Remove(Arg.Is<int>(100))
            .ReturnsForAnyArgs(a => a != null) // ReturnsForAnyArgs: Ignore arguments : Arg.Is<int>(100)
            .AndDoes(b => c++); // Add Call back

        Assert.AreEqual(true, _target.Remove(1));
        Assert.AreEqual(1, c);
    }

    [Test]
    public static void When_Called_Do_This()
    {
        // Returns() can be used to get callbacks for members that return a value
        // but for void members we need a different technique

        var c = 0;

        // The argument to the function is the substitute itself
        _target.When(t => t.IsEmpty()).Do(t => c++);

        _target.IsEmpty();
        _target.IsEmpty();

        Assert.That(c, Is.EqualTo(2));
    }

    [Test]
    public static void Throwing_Exceptions()
    {
        // For non-voids:
        // Return from a function: (int)arg[0]
        _target.Remove(Arg.Is<int>(i => i > 0)).Returns(b => { throw new Exception(); });

        // For voids:
        _target.When(t => t.Remove(Arg.Is<int>(i => i < 0))).Do((t) => { throw new Exception(); });

        //Both calls will now throw.
        Assert.Throws<Exception>(() => _target.Remove(1));
        Assert.Throws<Exception>(() => _target.Remove(-1));
    }

    #endregion


    #region Match Received

    [Test]
    public static void Received_Through_Raise_Event()
    {
        // if we want to check if _target.OnChanged triggered,
        // just set a custom callback do something then check
        _target.OnChanged += () => { _target.IsEmpty(); };

        // Trigger event automatically,
        _target.OnChanged += Raise.Event<Action>();

        // Checking

        _target.Received().IsEmpty();

        _target.DidNotReceiveWithAnyArgs().Remove(0);

        // Reset received calls
        _target.ClearReceivedCalls();

        _target.DidNotReceive().IsEmpty();
    }

    [Test]
    public static void Dictionary_Conditionally_Received()
    {
        var dictionary = Substitute.For<IDictionary<string, int>>();
        dictionary["test"] = 1;

        dictionary.Received()["test"] = 1;

        // Conditionally matching an argument
        dictionary.Received()["test"] = Arg.Is<int>(x => x < 5);
    }

    [Test]
    public static void Checking_Event_Subscriptions()
    {
        _target.OnChanged += () => Console.WriteLine("HAHA");

        _target.Received(1).OnChanged += Arg.Any<Action>();
    }

    [Test]
    public static void Checking_Call_Order()
    {
        _target.OnChanged += () => _target.IsEmpty();
        _target.IsEmpty();
        _target.OnChanged += () => Console.WriteLine("HAHA");

        Received.InOrder(() =>
        {
            _target.OnChanged += Arg.Any<Action>();
            _target.IsEmpty();
            _target.OnChanged += Arg.Any<Action>();
        });
    }

    [Test]
    public static void Callback_Builder()
    {
        // The Callback builder lets us create more complex Do() scenarios.
        // We can use Callback.First() followed by Then(), ThenThrow() and ThenKeepDoing() to build chains of callbacks.

        var sub = Substitute.For<IQueueValue<int>>();

        var calls = new List<string>();
        var counter = 0;

        sub
            .When(x => x.IsEmpty())
            .Do(
                Callback.First(x => calls.Add("1"))
                    .Then(x => calls.Add("2"))
                    .Then(x => calls.Add("3"))
                    .ThenKeepDoing(x => calls.Add("+"))
                    .AndAlways(x => counter++)
            );

        for (int i = 0; i < 5; i++)
        {
            sub.IsEmpty();
        }

        Assert.That(counter, Is.EqualTo(5));
        Assert.That(calls, Is.EqualTo(new List<String>(){"1", "2", "3", "+", "+"}));
    }

    #endregion

    #region Actions with argument matchers

    public interface IOrderProcessor
    {
        void ProcessOrder(int orderId, Action<bool> orderProcessed);
    }

    public interface IEvents
    {
        void RaiseOrderProcessed(int orderId);
    }

    public interface ICart
    {
        int OrderId { get; set; }
    }

    public class OrderPlacedCommand
    {
        IOrderProcessor orderProcessor;
        IEvents events;

        public OrderPlacedCommand(IOrderProcessor orderProcessor, IEvents events)
        {
            this.orderProcessor = orderProcessor;
            this.events = events;
        }

        public void Execute(ICart cart)
        {
            orderProcessor.ProcessOrder(
                cart.OrderId,
                wasOk =>
                {
                    if (wasOk) events.RaiseOrderProcessed(cart.OrderId);
                }
            );
        }
    }

    [Test]
    public static void Invoking_Callbacks()
    {
        //Arrange
        var cart = Substitute.For<ICart>();
        var events = Substitute.For<IEvents>();
        var processor = Substitute.For<IOrderProcessor>();
        cart.OrderId = 3;

        //Arrange for processor to invoke the callback arg with `true` whenever processing order id 3
        processor.ProcessOrder(3, Arg.Invoke(true));

        //Act
        var command = new OrderPlacedCommand(processor, events);
        command.Execute(cart);

        //Assert
        events.Received().RaiseOrderProcessed(3);
    }

    [Test]
    public static void Performing_Actions_With_Arguments()
    {
        var argumentUsed = 0;

        _target.Enqueue(Arg.Do<int>(i =>
            argumentUsed = i));

        _target.Enqueue(1);

        Assert.AreEqual(1, argumentUsed);
    }

    [Test]
    public static void Argument_Actions_And_Call_Specification()
    {
        //Specify a call where the first arg is less than 0, and the second is any int.
        //When this specification is met we'll increment a counter in the Arg.Do action for
        //the second argument that was used for the call, and we'll also make it return 123.

        var numberOfCallsWhereFirstArgIsLessThan0 = 0;

        var calculator = Substitute.For<ICalculator>();

        calculator
            .Multiply(
                Arg.Is<int>(x => x < 0),
                Arg.Do<int>(x => numberOfCallsWhereFirstArgIsLessThan0++)
            )
            .Returns(123);

        var results = new[]
        {
            calculator.Multiply(-4, 3),
            calculator.Multiply(-27, 88),
            calculator.Multiply(-7, 8),
            calculator.Multiply(123, 2) //First arg greater than 0, so spec won't be met.
        };

        Assert.AreEqual(3, numberOfCallsWhereFirstArgIsLessThan0); //3 of 4 calls have first arg < 0
        Assert.AreEqual(results, new[] {123, 123, 123, 0}); //Last call returns 0, not 123
    }

    #endregion

    #region Others

    [Test]
    public static void How_Not_To_Use_Argument_Matchers()
    {
        // Argument matchers should only be used when setting return values or checking received calls.
        // Using Arg.Is or Arg.Any without a call to .Returns or Received() can cause your tests to behave in unexpected ways.

        /* ARRANGE */

        // OK: Use arg matcher for a return value:
        _target.Remove(Arg.Is<int>(x => x > 10)).Returns(true);

        /* ACT */

        // NOT OK: arg matcher used with a real call:
        // _target.Remove(Arg.Any<int>());

        // Use a real argument instead:
        _target.Remove(11);

        /* ASSERT */

        // OK: Use arg matcher to check a call was received:
        _target.Received().Remove(Arg.Is<int>(x => x > 10));
    }

    [Test]
    public static void Auto_And_Recursive_Mocks()
    {
        //Once a substitute has been created some properties and methods will automatically return non-null values.
        //Other types, like String and Array, will default to returning empty values rather than nulls.

        var firstCall = _target.Clone(",");
        var secondCall = _target.Clone(",");

        // If a method is called with different arguments a new substitute will be returned.
        var thirdCallWithDiffArg = _target.Clone("X");

        Assert.AreSame(firstCall, secondCall);
        Assert.AreNotSame(firstCall, thirdCallWithDiffArg);
    }

    [Test]
    public static void Partial_Substitude()
    {

        // Instead of Substitute.For, Partial substitutes will be calling your class’ real code by default

/*      var stream = Substitute.ForPartsOf<StreamReader>(@"C:\Test.text");
        var stream = Substitute.For<StreamReader>(@"C:\Test.text");

        stream.ReadToEnd().Returns("asd");

        var str = stream.ReadToEnd();

        Assert.AreEqual(str, "asd");*/
    }

    #endregion
}