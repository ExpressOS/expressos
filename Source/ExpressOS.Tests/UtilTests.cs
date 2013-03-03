using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpressOS.Tests
{
    [TestClass]
    public class UtilTest
    {
        [TestMethod]
        public void ffsTest()
        {
            Assert.AreEqual<int>(0, Util.ffs(0));
            Assert.AreEqual<int>(1, Util.ffs(1));
            Assert.AreEqual<int>(2, Util.ffs(2));
            Assert.AreEqual<int>(1, Util.ffs(3));
        }

        [TestMethod]
        public void msbTest()
        {
            Assert.AreEqual<int>(0, Util.msb(0));
            Assert.AreEqual<int>(1, Util.msb(1));
            Assert.AreEqual<int>(2, Util.msb(2));
            Assert.AreEqual<int>(2, Util.msb(3));
        }

        [TestMethod]
        public void FixBVTest()
        {
            var bv = new FixedSizeBitVector(32);
            bv.Set(1);
            bv.Set(9);
            var b = bv.FindNextOne(-1);
            Assert.AreEqual<int>(1, b);
            b = bv.FindNextOne(b);
            Assert.AreEqual<int>(9, b);
            b = bv.FindNextOne(b);
            Assert.AreEqual<int>(-1, b);
        }
    }
}
