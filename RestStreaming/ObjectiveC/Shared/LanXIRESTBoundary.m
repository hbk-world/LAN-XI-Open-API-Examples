//
//  LanXIRESTBoundary.m
//  LanXIInterface
//
//  Created by Per Boye Clausen on 16/09/13.
//  Copyright (c) 2013 Brüel & Kjær. All rights reserved.
//

#import "LanXIRESTBoundary.h"

@implementation LanXIRESTBoundary

/**
    Initializes the REST boundary with a given host name. Any subsequent calls to the object will use this hostname.
 
    @param host     The host name or IP address to send requests to through this instance.
 */
- (id) initWithHostname:(NSString *)host
{
    _host = host;
    
    return self;
}

#pragma mark -
#pragma mark Generic REST request
/**
    Sends a HTTP request to the host and receives a JSON response. The request may contain a body, and the response may be empty.
 
    @param path     The path on the host to send the request to. I.e. giving the path "/rest/rec" sends the request to http://[hostname]/rest/rec
    @param method   The HTTP method to use, i.e. "GET", "PUT" or "POST".
    @param body     Body to send with the request. May contain e.g. JSON data for PUT or POST requests. The body may be nil, indicating no body is sent (e.g. for GET requests).
 
    @return JSON serialization of the response body from the request. If the response is empty or an error has occurred, nil is returned.
 */
- (NSJSONSerialization*) requestWithPath:(NSString *)path
                                  method:(NSString *)method
                                    body:(NSData *)body
                                   error:(NSError**)error
{
    NSLog(@"RESTBoundary %@ %@%@", method, _host, path);
    // Construct a URL object and a request object
    NSURL *url = [[NSURL alloc] initWithScheme:@"http" host:_host path:path];
    NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url];
    
    // Set method and body of the request
    [request setHTTPMethod:method];
    [request setHTTPBody:body];
    
    // Prepare a response variable and send the request.
    NSURLResponse *response = nil;
    NSData *data = [NSURLConnection sendSynchronousRequest:request returningResponse:&response error:error];
    
    // If no data was received (may be due to no body being returned) or an error has occurred, return nil.
    if (!data || *error || ![data length])
    {
        return nil;
    }
    
    // The request returned a body. 
    return [NSJSONSerialization JSONObjectWithData:data options:kNilOptions error:error];
}

#pragma mark GET/PUT/POST request methods
/**
    Sends a GET request to the host and returns the JSON response.
 
    @param path     The path on the host to send the request to. I.e. giving the path "/rest/rec" sends the request to http://[hostname]/rest/rec
 
    @return JSON deserialization of the response body from the request. If the response is empty or an error has occurred, nil is returned.
 */
- (NSJSONSerialization*) getRequestWithPath:(NSString *)path
                                      error:(NSError**)error
{
    return [self requestWithPath:path method:@"GET" body:nil error:error];
}

/**
 Sends a PUT request to the host and returns the JSON response.
 
 @param path     The path on the host to send the request to. I.e. giving the path "/rest/rec" sends the request to http://[hostname]/rest/rec
 @param body     Body to send to the host. May be nil.
 
 @return JSON deserialization of the response body from the request. If the response is empty or an error has occurred, nil is returned.
 */
- (NSJSONSerialization*) putRequestWithPath:(NSString *)path
                                       body:(NSData *)body
                                      error:(NSError**)error
{
    return [self requestWithPath:path method:@"PUT" body:body error:error];
}

/**
 Sends a POST request to the host and returns the JSON response.
 
 @param path     The path on the host to send the request to. I.e. giving the path "/rest/rec" sends the request to http://[hostname]/rest/rec
 @param body     Body to send to the host. May be nil.
 
 @return JSON deserialization of the response body from the request. If the response is empty or an error has occurred, nil is returned.
 */
- (NSJSONSerialization*) postRequestWithPath:(NSString *)path
                                        body:(NSData *)body
                                       error:(NSError**)error
{
    return [self requestWithPath:path method:@"POST" body:body error:error];
}

#pragma mark -
#pragma mark Shorthand methods for various tasks
/**
    Waits for the recorder to be in the specified state. The state is polled from the LAN-XI module at 1s interval.
    When the state anticipated is reached, the call returns. If the anticipated state is not reached after 255s, the call returns.
    @param state The state anticipated.
    @return YES if the anticipated state was reached, NO if the max time has elapsed.
 */
- (BOOL) waitForRecorderState:(NSString *)state error:(NSError **)error
{
    unsigned int seconds = 1;
    BOOL result = NO;
    
    for (;;)
    {
        // Get the module state
        NSDictionary *dict = (id)[self getRequestWithPath:@"/rest/rec/onchange" error:error];
        NSLog(@"WaitForRecorderState: %@, got %@", state, dict[@"moduleState"]);
        
        // See if the state is the one anticipated
        if ([state compare:dict[@"moduleState"]] == NSOrderedSame)
        {
            // Return
            result = YES;
            break;
        }
        
        // See if max time has elapsed
        if (seconds > 255)
            break;
        
        // Wait and try again
        sleep(1);
        seconds++;
    };
    
    return result;
}

/**
    Waits for the inputStatus to be in the specified state. The state is polled from the LAN-XI module at 1s interval.
    When the state anticipated is reached, the call returns. If the anticipated state is not reached after 255s, the call returns.
    @param state the state anticipated
    @return YES if the anticipated state was reached, NO if the max time has elapsed.
 */
- (BOOL) waitForInputState:(NSString *)state error:(NSError **)error
{
    unsigned int seconds = 1;
    BOOL result = NO;
    
    for (;;)
    {
        // Get the module state
        NSDictionary *dict = (id)[self getRequestWithPath:@"/rest/rec/onchange" error:error];
        NSLog(@"WaitForInputStatus: %@, got %@", state, dict[@"inputStatus"]);
        
        // See if the state is the one anticipated
        if ([state compare:dict[@"inputStatus"]] == NSOrderedSame)
        {
            // Return
            result = YES;
            break;
        }
        
        // See if max time has elapsed
        if (seconds > 255)
            break;
        
        // Wait and try again
        sleep(1);
        seconds++;
    }
    
    return result;
}

/**
 Waits for the PTP to be in the specified state. The state is polled from the LAN-XI module at 1s interval.
 When the state anticipated is reached, the call returns. If the anticipated state is not reached after 255s, the call returns.
 @param state the state anticipated
 @return YES if the anticipated state was reached, NO if the max time has elapsed.
 */
- (BOOL) waitForPtpState:(NSString *)state error:(NSError **)error
{
    unsigned int seconds = 1;
    BOOL result = NO;
    
    for (;;)
    {
        // Get the module state
        NSDictionary *dict = (id)[self getRequestWithPath:@"/rest/rec/onchange" error:error];
        NSLog(@"WaitForPtpStatus: %@, got %@", state, dict[@"ptpStatus"]);
        
        // See if the state is the one anticipated
        if ([state compare:dict[@"ptpStatus"]] == NSOrderedSame)
        {
            // Return
            result = YES;
            break;
        }
        
        // See if max time has elapsed
        if (seconds > 255)
            break;
        
        // Wait and try again
        sleep(1);
        seconds++;
    }
    
    return result;
}

@end
