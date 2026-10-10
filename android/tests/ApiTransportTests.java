package org.leafreader;
import java.io.*;
import java.net.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;
import com.sun.net.httpserver.*;
import static org.leafreader.Models.*;

/** Exercises real HTTP and cancellation; only Android's URI helper is substituted. */
public final class ApiTransportTests {
 static int checks;
 static void check(boolean value,String text){if(!value)throw new AssertionError(text);checks++;System.out.println("PASS "+text);}
 static void reply(HttpExchange e,int status,String body)throws IOException{byte[] bytes=body.getBytes("UTF-8");e.sendResponseHeaders(status,bytes.length);try(OutputStream out=e.getResponseBody()){out.write(bytes);}}
 static final String FILES="{\"files\":[{\"id\":\"file1\",\"name\":\"中文 # 2.jpg\",\"version\":\"v1\",\"width\":600,\"height\":900}]}";
 static final String BUSY="{\"error\":{\"code\":\"SERVER_BUSY\",\"message\":\"共享服务繁忙，请稍后重试\"}}";
 static Book book(Server server,String name){Book b=new Book();b.id="remote:"+server.id+":"+name;b.server=server;Page cover=new Page();cover.id="cover";b.pages.add(cover);return b;}
 public static void main(String[] args)throws Exception{
  HttpServer host=HttpServer.create(new InetSocketAddress("127.0.0.1",0),0);ExecutorService workers=Executors.newCachedThreadPool();host.setExecutor(workers);AtomicInteger busy=new AtomicInteger(),denied=new AtomicInteger(),full=new AtomicInteger();CountDownLatch waiting=new CountDownLatch(1),release=new CountDownLatch(1);
  host.createContext("/v1/books/transient/pages",e->{reply(e,busy.incrementAndGet()<3?503:200,busy.get()<3?BUSY:FILES);});
  host.createContext("/v1/books/full/pages",e->{full.incrementAndGet();reply(e,503,BUSY);});
  host.createContext("/v1/books/denied/pages",e->{denied.incrementAndGet();reply(e,401,"{\"error\":{\"code\":\"AUTH_REQUIRED\",\"message\":\"授权失效，请重新配对\"}}");});
  host.createContext("/v1/books/wait/pages",e->{waiting.countDown();try{release.await(3,TimeUnit.SECONDS);reply(e,200,FILES);}catch(InterruptedException ex){Thread.currentThread().interrupt();}catch(IOException ignored){}finally{e.close();}});
  host.createContext("/slow",e->{try{Thread.sleep(250);reply(e,200,"{}");}catch(InterruptedException ex){Thread.currentThread().interrupt();}catch(IOException ignored){}finally{e.close();}});
  host.createContext("/v1/books/empty/pages",e->reply(e,200,"{\"files\":[]} "));
  host.start();Server server=new Server();server.id="server";server.address="http://127.0.0.1:"+host.getAddress().getPort();server.token="fixture-only";
  try{
   Book b=book(server,"transient");Api.Request req=new Api.Request();Api.loadPages(b,req);check(busy.get()==3&&b.pages.size()==1&&b.pages.get(0).id.equals("file1"),"busy directory open retries bounded GET requests and loads real JSON");check(req.connection==null,"successful transport releases request connection");
   req=new Api.Request();try{Api.loadPages(book(server,"denied"),req);throw new AssertionError("authentication must fail");}catch(Api.Failure ex){check(ex.status==401&&denied.get()==1,"authorization failures are never retried");}check(req.connection==null,"HTTP failures release request connection");
   try{Api.loadPages(book(server,"full"),new Api.Request());throw new AssertionError("busy must fail");}catch(Api.Failure ex){check(ex.code.equals("SERVER_BUSY")&&full.get()==3,"permanently busy server stops after three attempts");}
   try{Api.loadPages(book(server,"empty"),new Api.Request());throw new AssertionError("empty must fail");}catch(IOException ex){check(ex.getMessage().contains("没有图片"),"empty directory gives a readable error");}
   Api.Request cancelled=new Api.Request();cancelled.cancel();try{Api.loadPages(book(server,"wait"),cancelled);throw new AssertionError("cancel must fail");}catch(InterruptedIOException ex){check(true,"cancelled open is rejected before network access");}
   final Api.Request pending=new Api.Request();final Book old=book(server,"wait");Future<?> opening=workers.submit(()->{try{Api.loadPages(old,pending);throw new AssertionError("cancelled load was accepted");}catch(Exception ex){if(!pending.cancelled)throw new RuntimeException(ex);}});check(waiting.await(2,TimeUnit.SECONDS),"directory request reaches actual HTTP server");check(pending.connection.getReadTimeout()==60000,"directory metadata has its own 60-second read allowance");Future<?> cancel=workers.submit(pending::cancel);for(int i=0;i<100&&!pending.cancelled;i++)Thread.sleep(10);check(pending.cancelled,"cancellation flag is set before late response delivery");release.countDown();cancel.get(3,TimeUnit.SECONDS);opening.get(3,TimeUnit.SECONDS);check(old.pages.get(0).id.equals("cover"),"late directory response cannot replace cancelled book pages");check(pending.connection==null,"cancelled in-flight transport releases connection");
   try{Api.json(server,"/slow",null,new Api.Request(),50);throw new AssertionError("must time out");}catch(SocketTimeoutException expected){check(true,"slow response times out using configured read timeout");}check(Api.json(server,"/slow",null,new Api.Request(),1000)!=null,"longer bounded read allowance accepts delayed response");
   HttpURLConnection first=(HttpURLConnection)new URL(server.address).openConnection(),second=(HttpURLConnection)new URL(server.address).openConnection();Api.Request ownership=new Api.Request();ownership.bind(first);ownership.bind(second);ownership.release(first);check(ownership.connection==second,"completion of old connection cannot clear newer cancellation handle");ownership.cancel();try{ownership.bind(first);throw new AssertionError("bind after cancel");}catch(InterruptedIOException ex){check(true,"cancel-before-bind race cannot attach a new connection");}first.disconnect();second.disconnect();
   System.out.println("ALL "+checks+" HTTP TRANSPORT TESTS PASSED");
  }finally{release.countDown();host.stop(0);workers.shutdownNow();}
 }
}
