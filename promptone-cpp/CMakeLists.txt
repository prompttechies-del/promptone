cmake_minimum_required(VERSION 3.20)
project(promptone_cpp VERSION 0.0.0 LANGUAGES CXX)

option(PROMPTONE_BUILD_TESTS "Build the GoogleTest suite" ON)
option(PROMPTONE_ONNX "Build the ONNX Runtime backend (not implemented yet)" OFF)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Floating point should match the Python reference: no fused multiply-add, no fast math.
# GCC and Clang get -ffp-contract=off. MSVC gets /fp:precise, which only disables fast-math
# reassociation and, before VS2022, does not by itself forbid contraction; the MSVC branch is
# untested and is not exercised by CI until the Windows lane (C11).
if(MSVC)
  set(PROMPTONE_FP_FLAGS /fp:precise)
  set(PROMPTONE_WARN_FLAGS /W4 /WX)
else()
  set(PROMPTONE_FP_FLAGS -ffp-contract=off -fno-fast-math)
  set(PROMPTONE_WARN_FLAGS -Wall -Wextra -Wpedantic -Werror)
endif()

function(promptone_target_flags target)
  target_compile_options(${target} PRIVATE ${PROMPTONE_FP_FLAGS} ${PROMPTONE_WARN_FLAGS})
endfunction()

add_library(promptone src/version.cpp src/sha256.cpp src/unicodedata.cpp src/pyjson.cpp src/pyvalue.cpp src/lang.cpp)
target_include_directories(promptone PUBLIC include)
promptone_target_flags(promptone)

if(PROMPTONE_ONNX)
  message(FATAL_ERROR "PROMPTONE_ONNX is not implemented yet")
endif()

if(PROMPTONE_BUILD_TESTS)
  include(FetchContent)
  # Pinned by commit, not tag: tags move. This is the commit of googletest v1.18.0.
  FetchContent_Declare(googletest
    GIT_REPOSITORY https://github.com/google/googletest.git
    GIT_TAG 063de7e9578f82b369302001269680b4b1553359)
  set(INSTALL_GTEST OFF CACHE BOOL "" FORCE)
  set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
  FetchContent_MakeAvailable(googletest)

  enable_testing()
  add_executable(promptone_tests tests/version_test.cpp tests/foundation_test.cpp tests/lang_test.cpp tests/hardening_test.cpp)
  target_link_libraries(promptone_tests PRIVATE promptone GTest::gtest_main)
  promptone_target_flags(promptone_tests)

  add_executable(promptone_cli tests/tools/promptone_cli.cpp)
  target_link_libraries(promptone_cli PRIVATE promptone)
  promptone_target_flags(promptone_cli)

  include(GoogleTest)
  # PRE_TEST: the parametrized rows are counted from the goldens, which exist at test time.
  gtest_discover_tests(promptone_tests DISCOVERY_MODE PRE_TEST DISCOVERY_TIMEOUT 120)

  # Listing must stay cheap (it runs before every ctest): bound it at 3 s with the goldens loaded.
  add_test(NAME discovery_fast
    COMMAND ${CMAKE_COMMAND} -DEXE=$<TARGET_FILE:promptone_tests> -DLIMIT=3
      -P ${CMAKE_CURRENT_SOURCE_DIR}/cmake/check_discovery_time.cmake)

  # compile_commands.json is the record of what the compiler was really given, so this fails if
  # the floating point flags are dropped from either target.
  # Spelled out here, not derived from PROMPTONE_FP_FLAGS, so dropping a flag above cannot also drop the
  # requirement.
  if(MSVC)
    set(PROMPTONE_FP_FLAGS_ARG "/fp:precise")
  else()
    set(PROMPTONE_FP_FLAGS_ARG "-ffp-contract=off|-fno-fast-math")
  endif()
  add_test(NAME compile_flags
    COMMAND ${CMAKE_COMMAND}
      -DCOMPILE_COMMANDS=${CMAKE_BINARY_DIR}/compile_commands.json
      -DSOURCE_DIR=${CMAKE_CURRENT_SOURCE_DIR}
      -DREQUIRED_FLAGS=${PROMPTONE_FP_FLAGS_ARG}
      -P ${CMAKE_CURRENT_SOURCE_DIR}/cmake/check_compile_flags.cmake)
endif()
