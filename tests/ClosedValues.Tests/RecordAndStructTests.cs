using System;
using System.Collections.Generic;
using Xunit;

namespace ClosedValues.Tests
{
    public sealed class RecordAndStructTests
    {
        [Fact]
        public void Record_GetsAllAndTheConstructor()
        {
            Assert.Equal([Season.Spring, Season.Summer, Season.Autumn, Season.Winter], Season.All);
            Assert.Equal("Autumn", Season.Autumn.Name);
            Assert.Equal(3, Season.Autumn.Order);
        }

        [Fact]
        public void Record_KeepsTheEqualityOfTheCompiler()
        {
            Assert.True(Season.Summer == Season.All[1]);
            Assert.True(Season.Summer != Season.Winter);
            Assert.Equal(Season.Summer.GetHashCode(), Season.All[1].GetHashCode());
        }

        [Fact]
        public void Record_ToString_ReturnsTheFirstStringMember()
        {
            Assert.Equal("Winter", Season.Winter.ToString());
        }

        [Fact]
        public void Record_Match_AcceptsACopyMadeWithWith()
        {
            Season copy = Season.Winter with { };

            Assert.NotSame(Season.Winter, copy);
            Assert.Equal("cold", copy.Match(spring: () => "mild", summer: () => "warm", autumn: () => "mild", winter: () => "cold"));
        }

        [Fact]
        public void Struct_GetsAllAndTheConstructor()
        {
            Assert.Equal([Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades], Suit.All);
            Assert.Equal("Hearts", Suit.Hearts.Name);
            Assert.True(Suit.Hearts.IsRed);
        }

        [Fact]
        public void Struct_Equality_ComparesTheMembers()
        {
            Assert.True(Suit.Hearts == Suit.All[2]);
            Assert.True(Suit.Hearts != Suit.Spades);
            Assert.True(Suit.Hearts.Equals((object)Suit.All[2]));
            Assert.False(Suit.Hearts.Equals((object)"Hearts"));
            Assert.Equal(Suit.Hearts.GetHashCode(), Suit.All[2].GetHashCode());
        }

        [Fact]
        public void Struct_WorksAsADictionaryKey()
        {
            Dictionary<Suit, char> symbols = new() { [Suit.Hearts] = 'h', [Suit.Spades] = 's' };

            Assert.Equal('h', symbols[Suit.All[2]]);
        }

        [Fact]
        public void Struct_ToString_ReturnsTheFirstStringMember()
        {
            Assert.Equal("Clubs", Suit.Clubs.ToString());
        }

        [Fact]
        public void Struct_Match_CallsTheFunctionOfTheInstance()
        {
            Suit copy = Suit.Diamonds;

            Assert.Equal(2, copy.Match(clubs: () => 1, diamonds: () => 2, hearts: () => 3, spades: () => 4));
        }

        [Fact]
        public void Struct_Default_HasNoMatchingInstance()
        {
            Suit[] uninitialized = new Suit[1];

            Assert.Throws<InvalidOperationException>(() => uninitialized[0].Match(clubs: () => 1, diamonds: () => 2, hearts: () => 3, spades: () => 4));
        }

        [Fact]
        public void RecordStruct_TryParse_FindsTheInstance()
        {
            Assert.True(Coin.TryParse(10, out Coin coin));
            Assert.Equal(Coin.Dime, coin);
        }

        [Fact]
        public void RecordStruct_TryParse_ReturnsTheDefaultWhenNothingMatches()
        {
            Assert.False(Coin.TryParse(3, out Coin coin));
            Assert.Equal(0, coin.Cents);
        }

        [Fact]
        public void RecordStruct_Match_CallsTheFunctionOfTheInstance()
        {
            Assert.Equal("quarter", Coin.Quarter.Match(penny: () => "penny", nickel: () => "nickel", dime: () => "dime", quarter: () => "quarter"));
        }
    }
}
