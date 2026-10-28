#include <gtest/gtest.h>

#include <string>

#include "promptone/version.hpp"

TEST(Version, IsSemver) {
  const std::string v = promptone::version();
  EXPECT_EQ(v, "0.0.0");
}
