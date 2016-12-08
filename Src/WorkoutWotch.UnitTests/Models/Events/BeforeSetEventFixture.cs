﻿namespace WorkoutWotch.UnitTests.Models.Events
{
    using WorkoutWotch.Models;
    using WorkoutWotch.Models.Events;
    using Xunit;

    public sealed class BeforeSetEventFixture
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(8)]
        [InlineData(13)]
        [InlineData(1628)]
        public void to_string_returns_correct_representation(int repetitions)
        {
            var sut = new BeforeSetEvent(new ExecutionContext(), repetitions);

            Assert.Equal("Before Set " + repetitions, sut.ToString());
        }
    }
}