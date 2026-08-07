from hase._generated import runtime_host_remote_api_v1_pb2
from hase._generated import runtime_host_remote_api_v1_pb2_grpc


def test_generated_message_module_imports() -> None:
    assert runtime_host_remote_api_v1_pb2.DESCRIPTOR.package == (
        "hase.runtime.remote.v1"
    )


def test_generated_grpc_module_imports() -> None:
    assert runtime_host_remote_api_v1_pb2_grpc.RuntimeHostRemoteApiStub is not None

