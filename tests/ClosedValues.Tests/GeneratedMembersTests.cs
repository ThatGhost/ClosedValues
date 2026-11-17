using Xunit;

namespace ClosedValues.Tests
{
    public sealed class GeneratedMembersTests
    {
        [Fact]
        public void All_ReturnsEveryInstanceInDeclarationOrder()
        {
            Assert.Equal(
                [Planet.Mercury, Planet.Venus, Planet.Earth, Planet.Mars, Planet.Jupiter],
                Planet.All);
        }

        [Fact]
        public void Constructor_AssignsTheMembersInDeclarationOrder()
        {
            Assert.Equal("Earth", Planet.Earth.Name);
            Assert.Equal(3, Planet.Earth.OrderFromSun);
        }

        [Fact]
        public void Equality_ComparesTheMembers()
        {
            Assert.True(Planet.Venus == Planet.All[1]);
            Assert.True(Planet.Venus != Planet.Earth);
            Assert.True(Planet.Venus.Equals((object)Planet.Venus));
            Assert.False(Planet.Venus.Equals(null));
            Assert.Equal(Planet.Venus.GetHashCode(), Planet.All[1].GetHashCode());
        }

        [Fact]
        public void ToString_ReturnsTheFirstStringMember()
        {
            Assert.Equal("Mars", Planet.Mars.ToString());
        }

        [Fact]
        public void Match_CallsTheFunctionOfTheInstance()
        {
            int moons = Planet.Mars.Match(
                mercury: () => 0,
                venus: () => 0,
                earth: () => 1,
                mars: () => 2,
                jupiter: () => 95);

            Assert.Equal(2, moons);
        }

        [Fact]
        public void Match_EscapesParameterNamesThatAreKeywords()
        {
            string modifier = Visibility.Public.Match(
                @default: () => string.Empty,
                @public: () => "public ",
                @private: () => "private ");

            Assert.Equal("public ", modifier);
        }

        [Fact]
        public void ComputedProperty_StaysUserCode()
        {
            Assert.True(Planet.Mars.IsInner);
            Assert.False(Planet.Jupiter.IsInner);
        }
    }
}
